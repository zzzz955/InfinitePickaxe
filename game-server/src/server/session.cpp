#include "session.h"
#include <iostream>
#include <cstring>
#include <ctime>
#include <cmath>

namespace {
uint32_t decode_le(const std::array<uint8_t,4>& buf) {
    return static_cast<uint32_t>(buf[0]) |
           (static_cast<uint32_t>(buf[1]) << 8) |
           (static_cast<uint32_t>(buf[2]) << 16) |
           (static_cast<uint32_t>(buf[3]) << 24);
}

std::array<uint8_t,4> encode_le(uint32_t v) {
    return {static_cast<uint8_t>(v & 0xFF),
            static_cast<uint8_t>((v >> 8) & 0xFF),
            static_cast<uint8_t>((v >> 16) & 0xFF),
            static_cast<uint8_t>((v >> 24) & 0xFF)};
}
} // namespace

Session::Session(boost::asio::ip::tcp::socket socket,
                 AuthService& auth_service,
                 GameRepository& game_repo,
                 MiningService& mining_service,
                 UpgradeService& upgrade_service,
                 MissionService& mission_service,
                 SlotService& slot_service,
                 OfflineService& offline_service)
    : socket_(std::move(socket)),
      auth_service_(auth_service),
      game_repo_(game_repo),
      mining_service_(mining_service),
      upgrade_service_(upgrade_service),
      mission_service_(mission_service),
      slot_service_(slot_service),
      offline_service_(offline_service) {
    init_router();
}

void Session::start() {
    try {
        client_ip_ = socket_.remote_endpoint().address().to_string();
    } catch (...) {
        client_ip_.clear();
    }
    read_length();
}

void Session::read_length() {
    auto self = shared_from_this();
    boost::asio::async_read(socket_, boost::asio::buffer(len_buf_),
        [this, self](boost::system::error_code ec, std::size_t /*len*/) {
            if (ec) {
                close();
                return;
            }
            uint32_t len = decode_le(len_buf_);
            if (len == 0 || len > 64 * 1024) { // 간단한 상한
                send_error("INVALID_LENGTH", "invalid length");
                close();
                return;
            }
            payload_buf_.resize(len);
            read_payload(len);
        });
}

void Session::read_payload(std::size_t length) {
    auto self = shared_from_this();
    boost::asio::async_read(socket_, boost::asio::buffer(payload_buf_.data(), length),
        [this, self](boost::system::error_code ec, std::size_t /*len*/) {
            if (ec) {
                close();
                return;
            }
            infinitepickaxe::Envelope env;
            if (!env.ParseFromArray(payload_buf_.data(), static_cast<int>(payload_buf_.size()))) {
                send_error("INVALID_ENVELOPE", "parse failed");
                close();
                return;
            }
            dispatch_envelope(env);
        });
}

bool Session::is_expired() const {
    if (expires_at_.time_since_epoch().count() == 0) return false;
    return std::chrono::system_clock::now() >= expires_at_;
}

void Session::dispatch_envelope(const infinitepickaxe::Envelope& env) {
    // 시퀀스 검증 (0이면 검증 생략) - 불일치 시 카운트 증가, 3회면 종료
    if (env.seq() != 0) {
        if (env.seq() != expected_seq_) {
            send_error("2002", "INVALID_SEQUENCE expected=" + std::to_string(expected_seq_) + " got=" + std::to_string(env.seq()));
            violation_count_++;
            if (violation_count_ >= 3) {
                close();
                return;
            }
        }
        expected_seq_ = env.seq() + 1;
    }

    // 타임스탬프 검증 (0이면 검증 생략) - 허용 범위 초과 시 카운트 증가, 3회면 종료
    if (env.timestamp() != 0) {
        uint64_t now = static_cast<uint64_t>(std::time(nullptr));
        uint64_t ts = env.timestamp();
        uint64_t diff = (now > ts) ? (now - ts) : (ts - now);
        const uint64_t MAX_DIFF = 60;
        if (diff > MAX_DIFF) {
            send_error("2003", "TIMESTAMP_MISMATCH diff=" + std::to_string(diff));
            violation_count_++;
            if (violation_count_ >= 3) {
                close();
                return;
            }
            return;
        }
    }

    if (is_expired()) {
        send_error("1003", "session expired");
        close();
        return;
    }
    const std::string& type = env.msg_type();

    if (type == "HANDSHAKE") {
        handle_handshake(env);
        return;
    }

    if (!authenticated_) {
        send_error("1001", "handshake required"); // AUTH_FAILED
        close();
        return;
    }

    if (!router_.dispatch(env)) {
        send_error("2001", type); // INVALID_PACKET / UNKNOWN_TYPE
    }

    // 다음 프레임 대기
    read_length();
}

void Session::handle_handshake(const infinitepickaxe::Envelope& env) {
    infinitepickaxe::HandshakeReq req;
    if (!req.ParseFromString(env.payload())) {
        send_error("2004", "handshake parse failed");
        return;
    }
    VerifyResult vr = auth_service_.verify_and_cache(req.jwt(), client_ip_);
    infinitepickaxe::HandshakeRes res;
    if (!vr.valid || vr.is_banned) {
        res.set_ok(false);
        res.set_error(vr.is_banned ? "1001" : "1001"); // AUTH_FAILED/BANNED
        send_envelope("HANDSHAKE_RES", res);
        close();
        return;
    }
    if (is_expired()) {
        res.set_ok(false);
        res.set_error("1003");
        send_envelope("HANDSHAKE_RES", res);
        close();
        return;
    }
    if (!game_repo_.ensure_user_initialized(vr.user_id)) {
        res.set_ok(false);
        res.set_error("5001");
        send_envelope("HANDSHAKE_RES", res);
        close();
        return;
    }
    user_id_ = vr.user_id;
    device_id_ = vr.device_id;
    google_id_ = vr.google_id;
    expires_at_ = vr.expires_at;
    authenticated_ = true;

    res.set_ok(true);
    res.set_user_id(vr.user_id);
    res.set_device_id(vr.device_id);
    res.set_google_id(vr.google_id);
    send_envelope("HANDSHAKE_RES", res);
}

void Session::handle_heartbeat(const infinitepickaxe::Envelope& env) {
    infinitepickaxe::Heartbeat hb;
    hb.ParseFromString(env.payload());
    infinitepickaxe::HeartbeatAck ack;
    ack.set_server_time_ms(
        static_cast<uint64_t>(
            std::chrono::duration_cast<std::chrono::milliseconds>(
                std::chrono::system_clock::now().time_since_epoch()).count()));
    send_envelope("HEARTBEAT_ACK", ack);
}

void Session::handle_mining(const infinitepickaxe::Envelope& env, const std::string& type) {
    if (type == "MINE_START") {
        infinitepickaxe::MiningStart req;
        if (!req.ParseFromString(env.payload())) {
            send_error("2004", "MINE_START parse failed");
            return;
        }
        auto upd = mining_service_.handle_start(req.mineral_id());
        send_envelope("MINE_UPDATE", upd);
    } else if (type == "MINE_SYNC") {
        infinitepickaxe::MiningSync req;
        if (!req.ParseFromString(env.payload())) {
            send_error("2004", "MINE_SYNC parse failed");
            return;
        }
        auto upd = mining_service_.handle_sync(req.mineral_id(), req.client_hp());
        send_envelope("MINE_UPDATE", upd);
    } else if (type == "MINE_COMPLETE") {
        infinitepickaxe::MiningComplete comp;
        if (!comp.ParseFromString(env.payload())) {
            send_error("2004", "MINE_COMPLETE parse failed");
            return;
        }
        auto res = mining_service_.handle_complete(user_id_, comp.mineral_id());
        send_envelope("MINE_COMPLETE", res);
    } else {
        infinitepickaxe::Error err;
        err.set_error_code("NOT_IMPLEMENTED");
        err.set_error_message("mining route not implemented");
        send_envelope("ERROR", err);
    }
}

void Session::handle_upgrade(const infinitepickaxe::Envelope& env) {
    infinitepickaxe::UpgradePickaxe req;
    if (!req.ParseFromString(env.payload())) {
        send_error("2004", "UPGRADE_PICKAXE parse failed");
        return;
    }
    auto res = upgrade_service_.handle_upgrade(user_id_, req.slot_index(), req.target_level());
    send_envelope("UPGRADE_RESULT", res);
}

void Session::handle_mission(const infinitepickaxe::Envelope& env, const std::string& type) {
    auto upd = mission_service_.build_stub_update();
    send_envelope("MISSION_UPDATE", upd);
}

void Session::handle_slot_unlock(const infinitepickaxe::Envelope& env) {
    infinitepickaxe::SlotUnlock req;
    if (!req.ParseFromString(env.payload())) {
        send_error("2004", "SLOT_UNLOCK parse failed");
        return;
    }
    auto res = slot_service_.handle_unlock(req.slot_index());
    send_envelope("SLOT_UNLOCK_RESULT", res);
}

void Session::handle_offline_reward(const infinitepickaxe::Envelope& env) {
    infinitepickaxe::OfflineRewardRequest req;
    if (!req.ParseFromString(env.payload())) {
        send_error("2004", "OFFLINE_REWARD_REQUEST parse failed");
        return;
    }
    auto res = offline_service_.handle_request();
    send_envelope("OFFLINE_REWARD", res);
}

void Session::init_router() {
    router_.register_handler("HEARTBEAT", [this](const infinitepickaxe::Envelope& e) { handle_heartbeat(e); });
    router_.register_handler("MINE_START", [this](const infinitepickaxe::Envelope& e) { handle_mining(e, "MINE_START"); });
    router_.register_handler("MINE_SYNC", [this](const infinitepickaxe::Envelope& e) { handle_mining(e, "MINE_SYNC"); });
    router_.register_handler("UPGRADE_PICKAXE", [this](const infinitepickaxe::Envelope& e) { handle_upgrade(e); });
    router_.register_handler("MISSION_CLAIM", [this](const infinitepickaxe::Envelope& e) { handle_mission(e, "MISSION_CLAIM"); });
    router_.register_handler("MISSION_REROLL", [this](const infinitepickaxe::Envelope& e) { handle_mission(e, "MISSION_REROLL"); });
    router_.register_handler("SLOT_UNLOCK", [this](const infinitepickaxe::Envelope& e) { handle_slot_unlock(e); });
    router_.register_handler("OFFLINE_REWARD_REQUEST", [this](const infinitepickaxe::Envelope& e) { handle_offline_reward(e); });
}

void Session::send_envelope(const std::string& msg_type, const google::protobuf::Message& msg) {
    infinitepickaxe::Envelope env;
    env.set_msg_type(msg_type);
    env.set_version(1);
    msg.SerializeToString(env.mutable_payload());

    std::string body;
    env.SerializeToString(&body);
    auto len = static_cast<uint32_t>(body.size());
    auto len_enc = encode_le(len);

    auto self = shared_from_this();
    std::array<boost::asio::const_buffer, 2> bufs = {
        boost::asio::buffer(len_enc),
        boost::asio::buffer(body)
    };
    boost::asio::async_write(socket_, bufs,
        [this, self](boost::system::error_code ec, std::size_t /*written*/) {
            if (ec) {
                close();
                return;
            }
        });
}

void Session::send_error(const std::string& code, const std::string& message) {
    infinitepickaxe::Error err;
    err.set_error_code(code);
    err.set_error_message(message);
    send_envelope("ERROR", err);
}

void Session::close() {
    boost::system::error_code ignored;
    socket_.shutdown(boost::asio::ip::tcp::socket::shutdown_both, ignored);
    socket_.close(ignored);
}
