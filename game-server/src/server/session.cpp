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
                 OfflineService& offline_service,
                 std::shared_ptr<SessionRegistry> registry)
    : socket_(std::move(socket)),
      auth_service_(auth_service),
      game_repo_(game_repo),
      mining_service_(mining_service),
      upgrade_service_(upgrade_service),
      mission_service_(mission_service),
      slot_service_(slot_service),
      offline_service_(offline_service),
      auth_timer_(socket_.get_executor()),
      registry_(std::move(registry)) {
    init_router();
}

void Session::start() {
    try {
        client_ip_ = socket_.remote_endpoint().address().to_string();
    } catch (...) {
        client_ip_.clear();
    }
    start_auth_timer();
    read_length();
}

void Session::notify_duplicate_and_close() {
    infinitepickaxe::ErrorNotification err;
    err.set_error_code("1006");
    err.set_message("DUPLICATE_SESSION");

    infinitepickaxe::Envelope env;
    env.set_type(infinitepickaxe::ERROR_NOTIFICATION);
    *env.mutable_error_notification() = err;

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
        [this, self](boost::system::error_code /*ec*/, std::size_t /*written*/) {
            close();
        });
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
        send_error("1003", "session expired");
        close();
        return;
    }

    if (env.type() == infinitepickaxe::HANDSHAKE) {
        handle_handshake(env);
        return;
    }

    if (!authenticated_) {
        send_error("1001", "handshake required");
        close();
        return;
    }

    if (!router_.dispatch(env)) {
        send_error("2001", "UNKNOWN_MESSAGE_TYPE");
    }

    // 다음 프레임 대기
    read_length();
}

void Session::handle_handshake(const infinitepickaxe::Envelope& env) {
    if (!env.has_handshake()) {
        send_error("2004", "handshake message missing");
        return;
    }
    const auto& req = env.handshake();
    VerifyResult vr = auth_service_.verify_and_cache(req.jwt(), client_ip_);

    infinitepickaxe::HandshakeResponse res;
    if (!vr.valid || vr.is_banned) {
        res.set_success(false);
        res.set_message(vr.is_banned ? "BANNED" : "AUTH_FAILED");

        infinitepickaxe::Envelope response_env;
        response_env.set_type(infinitepickaxe::HANDSHAKE_RESULT);
        *response_env.mutable_handshake_result() = res;
        send_envelope(response_env);
        close();
        return;
    }
    auto now = std::chrono::system_clock::now();
    if (vr.expires_at.time_since_epoch().count() != 0 && now >= vr.expires_at) {
        res.set_success(false);
        res.set_message("TOKEN_EXPIRED");

        infinitepickaxe::Envelope response_env;
        response_env.set_type(infinitepickaxe::HANDSHAKE_RESULT);
        *response_env.mutable_handshake_result() = res;
        send_envelope(response_env);
        close();
        return;
    }
    if (!game_repo_.ensure_user_initialized(vr.user_id)) {
        res.set_success(false);
        res.set_message("USER_INIT_FAILED");

        infinitepickaxe::Envelope response_env;
        response_env.set_type(infinitepickaxe::HANDSHAKE_RESULT);
        *response_env.mutable_handshake_result() = res;
        send_envelope(response_env);
        close();
        return;
    }
    user_id_ = vr.user_id;
    device_id_ = vr.device_id;
    google_id_ = vr.google_id;
    expires_at_ = vr.expires_at;
    authenticated_ = true;
    boost::system::error_code timer_ec;
    auth_timer_.cancel(timer_ec);

    if (registry_) {
        if (auto previous = registry_->replace_session(user_id_, shared_from_this())) {
            previous->notify_duplicate_and_close();
        }
    }

    res.set_success(true);
    res.set_message("OK");
    // TODO: UserDataSnapshot 추가 필요

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::HANDSHAKE_RESULT);
    *response_env.mutable_handshake_result() = res;
    send_envelope(response_env);
    read_length();
}

void Session::handle_heartbeat(const infinitepickaxe::Envelope& env) {
    if (!env.has_heartbeat()) {
        send_error("2004", "heartbeat message missing");
        return;
    }

    infinitepickaxe::HeartbeatAck ack;
    ack.set_server_time_ms(
        static_cast<uint64_t>(
            std::chrono::duration_cast<std::chrono::milliseconds>(
                std::chrono::system_clock::now().time_since_epoch()).count()));

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::HEARTBEAT_ACK);
    *response_env.mutable_heartbeat_ack() = ack;
    send_envelope(response_env);
}

void Session::handle_mining(const infinitepickaxe::Envelope& env) {
    infinitepickaxe::Envelope response_env;

    if (env.type() == infinitepickaxe::MINING_START) {
        if (!env.has_mining_start()) {
            send_error("2004", "mining_start message missing");
            return;
        }
        const auto& req = env.mining_start();
        auto upd = mining_service_.handle_start(user_id_, req.mineral_id());

        response_env.set_type(infinitepickaxe::MINING_UPDATE);
        *response_env.mutable_mining_update() = upd;
        send_envelope(response_env);

    } else if (env.type() == infinitepickaxe::MINING_SYNC) {
        if (!env.has_mining_sync()) {
            send_error("2004", "mining_sync message missing");
            return;
        }
        const auto& req = env.mining_sync();
        auto upd = mining_service_.handle_sync(user_id_, req.mineral_id(), req.client_hp());

        response_env.set_type(infinitepickaxe::MINING_UPDATE);
        *response_env.mutable_mining_update() = upd;
        send_envelope(response_env);

    } else if (env.type() == infinitepickaxe::MINING_COMPLETE) {
        if (!env.has_mining_complete()) {
            send_error("2004", "mining_complete message missing");
            return;
        }
        const auto& req = env.mining_complete();
        auto res = mining_service_.handle_complete(user_id_, req.mineral_id());

        response_env.set_type(infinitepickaxe::MINING_COMPLETE);
        *response_env.mutable_mining_complete() = res;
        send_envelope(response_env);

    } else {
        send_error("NOT_IMPLEMENTED", "unknown mining message type");
    }
}

void Session::handle_upgrade(const infinitepickaxe::Envelope& env) {
    if (!env.has_upgrade_request()) {
        send_error("2004", "upgrade_request message missing");
        return;
    }
    const auto& req = env.upgrade_request();
    auto res = upgrade_service_.handle_upgrade(user_id_, req.slot_index(), 0);

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::UPGRADE_RESULT);
    *response_env.mutable_upgrade_result() = res;
    send_envelope(response_env);
}

void Session::handle_mission(const infinitepickaxe::Envelope& env) {
    auto res = mission_service_.build_stub_update();

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::DAILY_MISSIONS_RESPONSE);
    *response_env.mutable_daily_missions_response() = res;
    send_envelope(response_env);
}

void Session::handle_slot_unlock(const infinitepickaxe::Envelope& env) {
    if (!env.has_slot_unlock()) {
        send_error("2004", "slot_unlock message missing");
        return;
    }
    const auto& req = env.slot_unlock();
    auto res = slot_service_.handle_unlock(user_id_, req.slot_index());

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::SLOT_UNLOCK_RESULT);
    *response_env.mutable_slot_unlock_result() = res;
    send_envelope(response_env);
}

void Session::handle_all_slots(const infinitepickaxe::Envelope& env) {
    auto res = slot_service_.handle_all_slots(user_id_);

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::ALL_SLOTS_RESPONSE);
    *response_env.mutable_all_slots_response() = res;
    send_envelope(response_env);
}

void Session::handle_offline_reward(const infinitepickaxe::Envelope& env) {
    if (!env.has_offline_reward_request()) {
        send_error("2004", "offline_reward_request message missing");
        return;
    }
    auto res = offline_service_.handle_request();

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::OFFLINE_REWARD_RESULT);
    *response_env.mutable_offline_reward_result() = res;
    send_envelope(response_env);
}

void Session::init_router() {
    router_.register_handler(infinitepickaxe::HEARTBEAT, [this](const infinitepickaxe::Envelope& e) { handle_heartbeat(e); });
    router_.register_handler(infinitepickaxe::MINING_START, [this](const infinitepickaxe::Envelope& e) { handle_mining(e); });
    router_.register_handler(infinitepickaxe::MINING_SYNC, [this](const infinitepickaxe::Envelope& e) { handle_mining(e); });
    router_.register_handler(infinitepickaxe::UPGRADE_REQUEST, [this](const infinitepickaxe::Envelope& e) { handle_upgrade(e); });
    router_.register_handler(infinitepickaxe::DAILY_MISSIONS_REQUEST, [this](const infinitepickaxe::Envelope& e) { handle_mission(e); });
    router_.register_handler(infinitepickaxe::SLOT_UNLOCK, [this](const infinitepickaxe::Envelope& e) { handle_slot_unlock(e); });
    router_.register_handler(infinitepickaxe::ALL_SLOTS_REQUEST, [this](const infinitepickaxe::Envelope& e) { handle_all_slots(e); });
    router_.register_handler(infinitepickaxe::OFFLINE_REWARD_REQUEST, [this](const infinitepickaxe::Envelope& e) { handle_offline_reward(e); });
}

void Session::send_envelope(const infinitepickaxe::Envelope& env) {
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
    infinitepickaxe::ErrorNotification err;
    err.set_error_code(code);
    err.set_message(message);

    infinitepickaxe::Envelope env;
    env.set_type(infinitepickaxe::ERROR_NOTIFICATION);
    *env.mutable_error_notification() = err;
    send_envelope(env);
}

void Session::close() {
    if (closed_) return;
    closed_ = true;
    boost::system::error_code timer_ec;
    auth_timer_.cancel(timer_ec);
    if (registry_ && !user_id_.empty()) {
        registry_->remove_if_match(user_id_, this);
    }
    boost::system::error_code ignored;
    socket_.shutdown(boost::asio::ip::tcp::socket::shutdown_both, ignored);
    socket_.close(ignored);
}

void Session::start_auth_timer() {
    auto self = shared_from_this();
    auth_timer_.expires_after(std::chrono::seconds(5));
    auth_timer_.async_wait([this, self](const boost::system::error_code& ec) {
        if (ec) return; // cancelled
        if (!authenticated_) {
            send_error("1007", "AUTH_TIMEOUT");
            close();
        }
    });
}
