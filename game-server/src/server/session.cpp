#include "session.h"
#include <iostream>
#include <cstring>

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
                 GameRepository& game_repo)
    : socket_(std::move(socket)),
      auth_service_(auth_service),
      game_repo_(game_repo) {}

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
    if (is_expired()) {
        send_error("TOKEN_EXPIRED", "session expired");
        close();
        return;
    }
    const std::string& type = env.msg_type();

    if (type == "HANDSHAKE") {
        infinitepickaxe::HandshakeReq req;
        if (!req.ParseFromString(env.payload())) {
            send_error("INVALID_PAYLOAD", "handshake parse failed");
            return;
        }
        VerifyResult vr = auth_service_.verify_and_cache(req.jwt(), client_ip_);
        infinitepickaxe::HandshakeRes res;
        if (!vr.valid || vr.is_banned) {
            res.set_ok(false);
            res.set_error(vr.is_banned ? "BANNED" : "AUTH_FAILED");
            send_envelope("HANDSHAKE_RES", res);
            close();
            return;
        }
        if (is_expired()) {
            res.set_ok(false);
            res.set_error("TOKEN_EXPIRED");
            send_envelope("HANDSHAKE_RES", res);
            close();
            return;
        }
        if (!game_repo_.ensure_user_initialized(vr.user_id)) {
            res.set_ok(false);
            res.set_error("DB_ERROR");
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
        // snapshot 채우기(필요하면 확장)
        send_envelope("HANDSHAKE_RES", res);
        return;
    }

    if (!authenticated_) {
        send_error("UNAUTHORIZED", "handshake required");
        close();
        return;
    }

    if (type == "HEARTBEAT") {
        infinitepickaxe::Heartbeat hb;
        hb.ParseFromString(env.payload());
        infinitepickaxe::HeartbeatAck ack;
        ack.set_server_time_ms(
            static_cast<uint64_t>(
                std::chrono::duration_cast<std::chrono::milliseconds>(
                    std::chrono::system_clock::now().time_since_epoch()).count()));
        send_envelope("HEARTBEAT_ACK", ack);
    } else if (type == "MINE_START") {
        infinitepickaxe::MiningStart req;
        if (!req.ParseFromString(env.payload())) {
            send_error("INVALID_PAYLOAD", "MINE_START parse failed");
            return;
        }
        infinitepickaxe::MiningUpdate upd;
        upd.set_mineral_id(req.mineral_id());
        upd.set_current_hp(0);
        upd.set_max_hp(0);
        upd.set_damage_dealt(0);
        upd.set_server_timestamp(static_cast<uint64_t>(std::time(nullptr)));
        send_envelope("MINE_UPDATE", upd);
    } else if (type == "MINE_SYNC") {
        infinitepickaxe::MiningSync req;
        if (!req.ParseFromString(env.payload())) {
            send_error("INVALID_PAYLOAD", "MINE_SYNC parse failed");
            return;
        }
        // TODO: 검증/치트 스코어 로직
        infinitepickaxe::MiningUpdate upd;
        upd.set_mineral_id(req.mineral_id());
        upd.set_current_hp(req.client_hp());
        upd.set_max_hp(req.client_hp());
        upd.set_damage_dealt(0);
        upd.set_server_timestamp(static_cast<uint64_t>(std::time(nullptr)));
        send_envelope("MINE_UPDATE", upd);
    } else if (type == "UPGRADE_PICKAXE") {
        infinitepickaxe::UpgradePickaxe req;
        if (!req.ParseFromString(env.payload())) {
            send_error("INVALID_PAYLOAD", "UPGRADE_PICKAXE parse failed");
            return;
        }
        infinitepickaxe::UpgradeResult res;
        res.set_success(false);
        res.set_error_code("NOT_IMPLEMENTED");
        send_envelope("UPGRADE_RESULT", res);
    } else if (type == "MISSION_CLAIM" || type == "MISSION_REROLL" ||
               type == "SLOT_UNLOCK" || type == "OFFLINE_REWARD_REQUEST" ||
               type == "MINE_PROGRESS" || type == "MINE_COMPLETE" || type == "MISSION_UPDATE") {
        infinitepickaxe::Error err;
        err.set_error_code("NOT_IMPLEMENTED");
        err.set_error_message("not implemented in MVP stub");
        send_envelope("ERROR", err);
    } else {
        infinitepickaxe::Error err;
        err.set_error_code("UNKNOWN_TYPE");
        err.set_error_message(type);
        send_envelope("ERROR", err);
    }

    // 다음 프레임 대기
    read_length();
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
