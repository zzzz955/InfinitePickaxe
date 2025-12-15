#include "session.h"
#include "metadata/metadata_loader.h"
#include "mission_repository.h"
#include <spdlog/spdlog.h>
#include <iostream>
#include <cstring>
#include <ctime>
#include <cmath>
#include <cstdlib>

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
                 std::shared_ptr<SessionRegistry> registry,
                 const MetadataLoader& metadata)
    : socket_(std::move(socket)),
      auth_service_(auth_service),
      game_repo_(game_repo),
      mining_service_(mining_service),
      upgrade_service_(upgrade_service),
      mission_service_(mission_service),
      slot_service_(slot_service),
      offline_service_(offline_service),
      auth_timer_(socket_.get_executor()),
      registry_(std::move(registry)),
      metadata_(metadata) {
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
            if (len == 0 || len > 64 * 1024) { // Í∞ÑÎã®???ÅÌïú
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

    // Îã§Ïùå Ìå®ÌÇ∑ÏùÑ Í≥ÑÏÜç ÏùΩÍ∏∞ ÏúÑÌï¥ Î£®ÌîÑÎ•º Ïù¥Ïñ¥Í∞ê (Ìï∏ÎìúÏÖ∞Ïù¥ÌÅ¨Îäî handle_handshake ÎÇ¥Î∂ÄÏóêÏÑú Ï≤òÎ¶¨)
    if (!closed_) {
        read_length();
    }
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

    // UserDataSnapshot Íµ¨ÏÑ±
    auto* snapshot = res.mutable_snapshot();

    // ?†Ï? Í≤åÏûÑ ?∞Ïù¥??Ï°∞Ìöå
    auto game_data = game_repo_.get_user_game_data(user_id_);
    snapshot->mutable_gold()->set_value(game_data.gold);
    snapshot->mutable_crystal()->set_value(game_data.crystal);

    // ?¨Î°Ø ?¥Í∏à ?ÅÌÉú
    for (bool unlocked : game_data.unlocked_slots) {
        snapshot->add_unlocked_slots(unlocked);
    }

    // ?ÑÏû¨ Ï±ÑÍµ¥ Ï§ëÏù∏ Í¥ëÎ¨º ?ïÎ≥¥ (DB?êÏÑú Ï°∞Ìöå, nullable Ï≤òÎ¶¨)
    if (game_data.current_mineral_id.has_value()) {
        const auto* mineral = metadata_.mineral(game_data.current_mineral_id.value());
        snapshot->mutable_current_mineral_id()->set_value(game_data.current_mineral_id.value());
        snapshot->mutable_mineral_hp()->set_value(game_data.current_mineral_hp.value_or(0));
        snapshot->mutable_mineral_max_hp()->set_value(mineral ? mineral->hp : 100);
    }

    // ?¨Î°Ø ?ïÎ≥¥ Î∞?Ï¥?DPS
    auto slots_response = slot_service_.handle_all_slots(user_id_);
    for (const auto& slot : slots_response.slots()) {
        *snapshot->add_pickaxe_slots() = slot;
    }
    snapshot->set_total_dps(slots_response.total_dps());

    // ?úÎ≤Ñ ?úÍ∞Ñ
    snapshot->mutable_server_time()->set_value(
        static_cast<uint64_t>(
            std::chrono::duration_cast<std::chrono::milliseconds>(
                std::chrono::system_clock::now().time_since_epoch()).count()));

    // Í¥ëÍ≥† Ïπ¥Ïö¥??Ï∂îÍ?
    auto ad_counters = mission_service_.get_ad_counters(user_id_);
    for (const auto& counter : ad_counters) {
        auto* ad_counter = snapshot->add_ad_counters();
        ad_counter->set_ad_type(counter.ad_type);
        ad_counter->set_ad_count(counter.ad_count);
        uint32_t limit = 0;
        if (const auto* ad_meta = metadata_.ad_meta(counter.ad_type)) {
            limit = ad_meta->daily_limit;
        }
        ad_counter->set_daily_limit(limit);
    }

    auto offline_state = offline_service_.get_state(user_id_);
    snapshot->set_current_offline_hours(offline_state.current_offline_seconds / 3600);

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::HANDSHAKE_RESULT);
    *response_env.mutable_handshake_result() = res;
    send_envelope(response_env);

    // Ï±ÑÍµ¥ ?úÎ??àÏù¥???úÏûë (DB?êÏÑú Î°úÎìú???ÑÏû¨ Í¥ëÎ¨ºÎ°? nullable Ï≤òÎ¶¨)
    if (game_data.current_mineral_id.has_value() && game_data.current_mineral_hp.has_value()) {
        mining_state_.current_mineral_id = game_data.current_mineral_id.value();
        mining_state_.current_hp = game_data.current_mineral_hp.value();
        const auto* current_mineral = metadata_.mineral(mining_state_.current_mineral_id);
        mining_state_.max_hp = current_mineral ? current_mineral->hp : 100;

        // Í¥ëÎ¨º HPÍ∞Ä 0???ÑÎãàÎ©?Ï±ÑÍµ¥ ?úÏûë
        if (mining_state_.current_hp > 0) {
            start_new_mineral();
        }
    } else {
        // Í¥ëÎ¨º???ÜÏúºÎ©?Í∏∞Î≥∏ Í¥ëÎ¨º(1Î≤?Î°??úÏûë
        mining_state_.current_mineral_id = 1;
        start_new_mineral();
    }

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

// ?úÎ≤Ñ Í∂åÏúÑ???ÑÌÇ§?çÏ≤òÎ°?Î≥ÄÍ≤ΩÎêò?????¥ÏÉÅ ?¨Ïö©?òÏ? ?äÏùå
// ?úÎ≤ÑÍ∞Ä ?êÎèô?ºÎ°ú Ï±ÑÍµ¥ ?úÎ??àÏù¥?òÏùÑ ?åÎ¶¨Í≥??¥Îùº?¥Ïñ∏?∏Îäî ?åÎçîÎßÅÎßå ?òÌñâ
// void Session::handle_mining(const infinitepickaxe::Envelope& env) {
//     infinitepickaxe::Envelope response_env;
//
//     if (env.type() == infinitepickaxe::MINING_START) {
//         if (!env.has_mining_start()) {
//             send_error("2004", "mining_start message missing");
//             return;
//         }
//         const auto& req = env.mining_start();
//         auto upd = mining_service_.handle_start(user_id_, req.mineral_id());
//
//         response_env.set_type(infinitepickaxe::MINING_UPDATE);
//         *response_env.mutable_mining_update() = upd;
//         send_envelope(response_env);
//
//     } else if (env.type() == infinitepickaxe::MINING_SYNC) {
//         if (!env.has_mining_sync()) {
//             send_error("2004", "mining_sync message missing");
//             return;
//         }
//         const auto& req = env.mining_sync();
//         auto upd = mining_service_.handle_sync(user_id_, req.mineral_id(), req.client_hp());
//
//         response_env.set_type(infinitepickaxe::MINING_UPDATE);
//         *response_env.mutable_mining_update() = upd;
//         send_envelope(response_env);
//
//     } else if (env.type() == infinitepickaxe::MINING_COMPLETE) {
//         if (!env.has_mining_complete()) {
//             send_error("2004", "mining_complete message missing");
//             return;
//         }
//         const auto& req = env.mining_complete();
//         auto res = mining_service_.handle_complete(user_id_, req.mineral_id());
//
//         response_env.set_type(infinitepickaxe::MINING_COMPLETE);
//         *response_env.mutable_mining_complete() = res;
//         send_envelope(response_env);
//
//     } else {
//         send_error("NOT_IMPLEMENTED", "unknown mining message type");
//     }
// }

void Session::handle_upgrade(const infinitepickaxe::Envelope& env) {
    if (!env.has_upgrade_request()) {
        send_error("2004", "upgrade_request message missing");
        return;
    }
    const auto& req = env.upgrade_request();
    // ?ÑÏû¨ ?¨Î°Ø ?àÎ≤®??Ï°∞Ìöå??target_level = current + 1 Î°??§Ï†ï
    auto slot = slot_service_.get_slot(user_id_, req.slot_index());
    infinitepickaxe::UpgradeResult res;
    if (!slot.has_value()) {
        res.set_success(false);
        res.set_slot_index(req.slot_index());
        res.set_error_code("3004"); // SLOT_NOT_FOUND
    } else {
        uint32_t target_level = slot->level + 1;
        res = upgrade_service_.handle_upgrade(user_id_, req.slot_index(), target_level);
    }

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::UPGRADE_RESULT);
    *response_env.mutable_upgrade_result() = res;
    send_envelope(response_env);
}

void Session::handle_mission(const infinitepickaxe::Envelope& env) {
    auto res = mission_service_.get_missions(user_id_);

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::DAILY_MISSIONS_RESPONSE);
    *response_env.mutable_daily_missions_response() = res;
    send_envelope(response_env);
}

void Session::handle_mission_progress_update(const infinitepickaxe::Envelope& env) {
    if (!env.has_mission_progress_update()) {
        send_error("2004", "mission_progress_update message missing");
        return;
    }
    const auto& req = env.mission_progress_update();
    mission_service_.update_mission_progress(user_id_, req.slot_no(), req.current_value());
}

void Session::handle_mission_complete(const infinitepickaxe::Envelope& env) {
    if (!env.has_mission_complete()) {
        send_error("2004", "mission_complete message missing");
        return;
    }
    const auto& req = env.mission_complete();
    auto res = mission_service_.claim_mission_reward(user_id_, req.slot_no());

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::MISSION_COMPLETE_RESULT);
    *response_env.mutable_mission_complete_result() = res;
    send_envelope(response_env);
}

void Session::handle_mission_reroll(const infinitepickaxe::Envelope& env) {
    auto res = mission_service_.reroll_missions(user_id_);

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::MISSION_REROLL_RESULT);
    *response_env.mutable_mission_reroll_result() = res;
    send_envelope(response_env);
}

void Session::handle_ad_watch(const infinitepickaxe::Envelope& env) {
    if (!env.has_ad_watch_complete()) {
        send_error("2004", "ad_watch_complete message missing");
        return;
    }
    const auto& req = env.ad_watch_complete();
    auto res = mission_service_.handle_ad_watch(user_id_, req.ad_type());

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::AD_WATCH_RESULT);
    *response_env.mutable_ad_watch_result() = res;
    send_envelope(response_env);
}

void Session::handle_milestone_claim(const infinitepickaxe::Envelope& env) {
    if (!env.has_milestone_claim()) {
        send_error("2004", "milestone_claim message missing");
        return;
    }


    const auto& req = env.milestone_claim();
    auto res = mission_service_.handle_milestone_claim(user_id_, req.milestone_count());

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::MILESTONE_CLAIM_RESULT);
    *response_env.mutable_milestone_claim_result() = res;
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
    auto res = offline_service_.handle_request(user_id_);

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::OFFLINE_REWARD_RESULT);
    *response_env.mutable_offline_reward_result() = res;
    send_envelope(response_env);
}

void Session::init_router() {
    router_.register_handler(infinitepickaxe::HEARTBEAT, [this](const infinitepickaxe::Envelope& e) { handle_heartbeat(e); });
    // ?úÎ≤Ñ Í∂åÏúÑ???ÑÌÇ§?çÏ≤òÎ°?Î≥ÄÍ≤ΩÎêò???¥Îùº?¥Ïñ∏?∏Í? ???¥ÏÉÅ MINING_START, MINING_SYNCÎ•?Î≥¥ÎÇ¥ÏßÄ ?äÏùå
    // router_.register_handler(infinitepickaxe::MINING_START, [this](const infinitepickaxe::Envelope& e) { handle_mining(e); });
    // router_.register_handler(infinitepickaxe::MINING_SYNC, [this](const infinitepickaxe::Envelope& e) { handle_mining(e); });
    router_.register_handler(infinitepickaxe::UPGRADE_REQUEST, [this](const infinitepickaxe::Envelope& e) { handle_upgrade(e); });
    router_.register_handler(infinitepickaxe::DAILY_MISSIONS_REQUEST, [this](const infinitepickaxe::Envelope& e) { handle_mission(e); });
    router_.register_handler(infinitepickaxe::MISSION_PROGRESS_UPDATE, [this](const infinitepickaxe::Envelope& e) { handle_mission_progress_update(e); });
    router_.register_handler(infinitepickaxe::MISSION_COMPLETE, [this](const infinitepickaxe::Envelope& e) { handle_mission_complete(e); });
    router_.register_handler(infinitepickaxe::MISSION_REROLL, [this](const infinitepickaxe::Envelope& e) { handle_mission_reroll(e); });
    router_.register_handler(infinitepickaxe::AD_WATCH_COMPLETE, [this](const infinitepickaxe::Envelope& e) { handle_ad_watch(e); });
    router_.register_handler(infinitepickaxe::MILESTONE_CLAIM, [this](const infinitepickaxe::Envelope& e) { handle_milestone_claim(e); });
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
void Session::update_mining_tick(float delta_ms) {
    // ?∏Ï¶ù?òÏ? ?äÏïòÍ±∞ÎÇò ?∏ÏÖò???´Ìòî?ºÎ©¥ Î¨¥Ïãú
    if (!authenticated_ || closed_) {
        return;
    }

    // Ï±ÑÍµ¥ Ï§ëÏù¥ ?ÑÎãàÎ©?Î¶¨Ïä§???Ä?¥Î®∏ Ï≤òÎ¶¨
    if (!mining_state_.is_mining) {
        if (mining_state_.respawn_timer_ms > 0) {
            mining_state_.respawn_timer_ms -= delta_ms;
            if (mining_state_.respawn_timer_ms <= 0) {
                // 5Ï¥??ÄÍ∏??ÑÎ£å ????Í¥ëÎ¨ºÎ°??êÎèô ?úÏûë
                start_new_mineral();
            }
        }
        return;
    }

    std::vector<infinitepickaxe::PickaxeAttack> attacks;

    for (auto& slot : mining_state_.slots) {
        slot.next_attack_timer_ms -= delta_ms;

        // 40ms ?ôÏïà ?¨Îü¨ Î≤?Í≥µÍ≤©?????àÏùå (attack_speedÍ∞Ä Îß§Ïö∞ Îπ†Î•∏ Í≤ΩÏö∞)
        while (slot.next_attack_timer_ms <= 0) {
            infinitepickaxe::PickaxeAttack attack;
            attack.set_slot_index(slot.slot_index);
            attack.set_damage(slot.attack_power);
            attacks.push_back(attack);

            // ?§Ïùå Í≥µÍ≤© ?úÍ∞Ñ ?§Ï†ï (Î∞ÄÎ¶¨Ï¥à)
            // attack_speed = attacks per second
            // attack_interval_ms = 1000 / attack_speed
            float attack_interval_ms = 1000.0f / slot.attack_speed;
            slot.next_attack_timer_ms += attack_interval_ms;
        }
    }

    // HP Í∞êÏÜå
    uint64_t total_damage = 0;
    for (const auto& attack : attacks) {
        total_damage += attack.damage();
    }

    if (total_damage > 0) {
        if (mining_state_.current_hp > total_damage) {
            mining_state_.current_hp -= total_damage;
        } else {
            mining_state_.current_hp = 0;
        }
    }

    // Ï±ÑÍµ¥ ?ÑÎ£å Ï≤¥ÌÅ¨
    if (mining_state_.current_hp == 0) {
        // Ï¶âÏãú Ï±ÑÍµ¥ ?ÑÎ£å ?∏Ïãú (?±Í≥º Î¨¥Í?)
        handle_mining_complete_immediate();
        return;
    }

    // MiningUpdate ?ÑÏÜ° (Í≥µÍ≤©???ÜÏñ¥???ÑÏÜ° - ?¥Îùº?¥Ïñ∏???ôÍ∏∞??
    send_mining_update(attacks);
}

void Session::start_new_mineral() {
    // ??Í¥ëÎ¨ºÎ°??úÏûë (?ÑÏû¨???ôÏùº Í¥ëÎ¨º ?¨Ïãú??
    // TODO: Í¥ëÎ¨º ?†ÌÉù Î°úÏßÅ Ï∂îÍ? Í∞Ä??
    // Î©îÌ??∞Ïù¥?∞Ïóê??Í¥ëÎ¨º ?ïÎ≥¥ Ï°∞Ìöå
    const auto* mineral = metadata_.mineral(mining_state_.current_mineral_id);
    if (!mineral) {
        spdlog::error("Invalid mineral_id: {}", mining_state_.current_mineral_id);
        return;
    }

    mining_state_.current_hp = mineral->hp;
    mining_state_.max_hp = mineral->hp;
    mining_state_.is_mining = true;
    mining_state_.respawn_timer_ms = 0;

    // ?¨Î°Ø ?ïÎ≥¥ Î°úÎìú (DB?êÏÑú)
    auto slots_response = slot_service_.handle_all_slots(user_id_);
    mining_state_.slots.clear();

    for (const auto& slot_info : slots_response.slots()) {
        if (slot_info.is_unlocked() && slot_info.level() > 0) {
            SlotMiningState slot;
            slot.slot_index = slot_info.slot_index();
            slot.attack_power = slot_info.attack_power();
            slot.attack_speed = slot_info.attack_speed_x100() / 100.0f;  // 100 ??1.0 APS

            // Ï¥àÍ∏∞ Í≥µÍ≤© ?Ä?¥Î®∏: ?úÎç§?òÍ≤å Î∂ÑÏÇ∞ (Î™®Îì† ?¨Î°Ø???ôÏãú??Í≥µÍ≤©?òÏ? ?äÎèÑÎ°?
            slot.next_attack_timer_ms = (float)(std::rand() % 1000) / 1000.0f * (1000.0f / slot.attack_speed);

            mining_state_.slots.push_back(slot);
        }
    }

    spdlog::info("Mining started: user={} mineral={} hp={} slots={}",
                 user_id_, mining_state_.current_mineral_id, mining_state_.current_hp, mining_state_.slots.size());
}

void Session::send_mining_update(const std::vector<infinitepickaxe::PickaxeAttack>& attacks) {
    infinitepickaxe::MiningUpdate update;
    update.set_mineral_id(mining_state_.current_mineral_id);
    update.set_current_hp(mining_state_.current_hp);
    update.set_max_hp(mining_state_.max_hp);
    update.set_server_timestamp(static_cast<uint64_t>(
        std::chrono::duration_cast<std::chrono::milliseconds>(
            std::chrono::system_clock::now().time_since_epoch()).count()));

    for (const auto& attack : attacks) {
        *update.add_attacks() = attack;
    }

    infinitepickaxe::Envelope env;
    env.set_type(infinitepickaxe::MINING_UPDATE);
    *env.mutable_mining_update() = update;
    send_envelope(env);
}

void Session::handle_mining_complete_immediate() {
    // Ï±ÑÍµ¥ ?ÑÎ£å Ï≤òÎ¶¨
    mining_state_.is_mining = false;

    // Î©îÌ??∞Ïù¥?∞Ïóê??Î≥¥ÏÉÅ Ï°∞Ìöå
    const auto* mineral = metadata_.mineral(mining_state_.current_mineral_id);
    if (!mineral) {
        spdlog::error("Invalid mineral_id: {}", mining_state_.current_mineral_id);
        return;
    }

    uint64_t gold_reward = mineral->reward;
    uint32_t respawn_time_sec = mineral->respawn_time;

    auto completion_result = mining_service_.handle_complete(user_id_, mining_state_.current_mineral_id);

    // Î¶¨Ïä§???Ä?¥Î®∏ ?úÏûë
    mining_state_.respawn_timer_ms = respawn_time_sec * 1000.0f;

    // ?¥Îùº?¥Ïñ∏?∏ÏóêÍ≤?MiningComplete Ï¶âÏãú ?ÑÏÜ°
    infinitepickaxe::MiningComplete complete;
    complete.set_mineral_id(mining_state_.current_mineral_id);
    complete.set_gold_earned(completion_result.gold_earned());
    complete.set_total_gold(completion_result.total_gold());
    complete.set_mining_count(completion_result.mining_count());
    complete.set_respawn_time(respawn_time_sec);
    complete.set_server_timestamp(static_cast<uint64_t>(
        std::chrono::duration_cast<std::chrono::milliseconds>(
            std::chrono::system_clock::now().time_since_epoch()).count()));

    infinitepickaxe::Envelope env;
    env.set_type(infinitepickaxe::MINING_COMPLETE);
    *env.mutable_mining_complete() = complete;
    send_envelope(env);

    spdlog::info("Mining completed: user={} mineral={} gold_earned={} respawn_time={}s",
                 user_id_, mining_state_.current_mineral_id, gold_reward, respawn_time_sec);
}
