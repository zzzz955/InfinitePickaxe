#include "session.h"
#include "metadata/metadata_loader.h"
#include "mission_repository.h"
#include <spdlog/spdlog.h>
#include <iostream>
#include <cstring>
#include <ctime>
#include <cmath>
#include <cstdlib>
#include <random>
#include <limits>
#include <algorithm>

namespace
{
    uint32_t decode_le(const std::array<uint8_t, 4> &buf)
    {
        return static_cast<uint32_t>(buf[0]) |
               (static_cast<uint32_t>(buf[1]) << 8) |
               (static_cast<uint32_t>(buf[2]) << 16) |
               (static_cast<uint32_t>(buf[3]) << 24);
    }

    std::array<uint8_t, 4> encode_le(uint32_t v)
    {
        return {static_cast<uint8_t>(v & 0xFF),
                static_cast<uint8_t>((v >> 8) & 0xFF),
                static_cast<uint8_t>((v >> 16) & 0xFF),
                static_cast<uint8_t>((v >> 24) & 0xFF)};
    }

    uint32_t roll_bp_10000()
    {
        static thread_local std::mt19937 rng(std::random_device{}());
        static thread_local std::uniform_int_distribution<uint32_t> dist(0, 9999);
        return dist(rng);
    }

    constexpr int kMiningCacheTtlSeconds = 60 * 60 * 24;

    bool parse_u64(const std::string& value, uint64_t& out)
    {
        if (value.empty())
        {
            return false;
        }
        char* end = nullptr;
        unsigned long long v = std::strtoull(value.c_str(), &end, 10);
        if (!end || *end != '\0')
        {
            return false;
        }
        out = static_cast<uint64_t>(v);
        return true;
    }

    bool parse_u32(const std::string& value, uint32_t& out)
    {
        if (value.empty())
        {
            return false;
        }
        char* end = nullptr;
        unsigned long v = std::strtoul(value.c_str(), &end, 10);
        if (!end || *end != '\0' || v > std::numeric_limits<uint32_t>::max())
        {
            return false;
        }
        out = static_cast<uint32_t>(v);
        return true;
    }
} // namespace

Session::Session(boost::asio::ip::tcp::socket socket,
                 AuthService &auth_service,
                 GameRepository &game_repo,
                 MiningService &mining_service,
                 UpgradeService &upgrade_service,
                 MissionService &mission_service,
                 SlotService &slot_service,
                 OfflineService &offline_service,
                 RedisClient &redis_client,
                 std::shared_ptr<SessionRegistry> registry,
                 const MetadataLoader &metadata)
    : socket_(std::move(socket)),
      auth_service_(auth_service),
      game_repo_(game_repo),
      mining_service_(mining_service),
      upgrade_service_(upgrade_service),
      mission_service_(mission_service),
      slot_service_(slot_service),
      offline_service_(offline_service),
      redis_(redis_client),
      auth_timer_(socket_.get_executor()),
      registry_(std::move(registry)),
      metadata_(metadata)
{
    init_router();
}

void Session::start()
{
    try
    {
        client_ip_ = socket_.remote_endpoint().address().to_string();
    }
    catch (...)
    {
        client_ip_.clear();
    }
    start_auth_timer();
    read_length();
}

void Session::notify_duplicate_and_close()
{
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
        boost::asio::buffer(body)};
    boost::asio::async_write(socket_, bufs,
                             [this, self](boost::system::error_code /*ec*/, std::size_t /*written*/)
                             {
                                 close();
                             });
}

void Session::read_length()
{
    auto self = shared_from_this();
    boost::asio::async_read(socket_, boost::asio::buffer(len_buf_),
                            [this, self](boost::system::error_code ec, std::size_t /*len*/)
                            {
                                if (ec)
                                {
                                    close();
                                    return;
                                }
                                uint32_t len = decode_le(len_buf_);
                                if (len == 0 || len > 64 * 1024)
                                { // 간단???�한
                                    send_error("INVALID_LENGTH", "invalid length");
                                    close();
                                    return;
                                }
                                payload_buf_.resize(len);
                                read_payload(len);
                            });
}

void Session::read_payload(std::size_t length)
{
    auto self = shared_from_this();
    boost::asio::async_read(socket_, boost::asio::buffer(payload_buf_.data(), length),
                            [this, self](boost::system::error_code ec, std::size_t /*len*/)
                            {
                                if (ec)
                                {
                                    close();
                                    return;
                                }
                                infinitepickaxe::Envelope env;
                                if (!env.ParseFromArray(payload_buf_.data(), static_cast<int>(payload_buf_.size())))
                                {
                                    send_error("INVALID_ENVELOPE", "parse failed");
                                    close();
                                    return;
                                }
                                dispatch_envelope(env);
                            });
}

bool Session::is_expired() const
{
    if (expires_at_.time_since_epoch().count() == 0)
        return false;
    return std::chrono::system_clock::now() >= expires_at_;
}

void Session::dispatch_envelope(const infinitepickaxe::Envelope &env)
{
    if (is_expired())
    {
        send_error("1003", "session expired");
        close();
        return;
    }

    if (env.type() == infinitepickaxe::HANDSHAKE)
    {
        handle_handshake(env);
        return;
    }

    if (!authenticated_)
    {
        send_error("1001", "handshake required");
        close();
        return;
    }

    if (!router_.dispatch(env))
    {
        send_error("2001", "UNKNOWN_MESSAGE_TYPE");
    }

    // 다음 패킷을 계속 읽기 위해 루프를 이어감 (핸드셰이크는 handle_handshake 내부에서 처리)
    if (!closed_)
    {
        read_length();
    }
}

void Session::handle_handshake(const infinitepickaxe::Envelope &env)
{
    if (!env.has_handshake())
    {
        send_error("2004", "handshake message missing");
        return;
    }
    const auto &req = env.handshake();
    VerifyResult vr = auth_service_.verify_and_cache(req.jwt(), client_ip_);

    infinitepickaxe::HandshakeResponse res;
    if (!vr.valid || vr.is_banned)
    {
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
    if (vr.expires_at.time_since_epoch().count() != 0 && now >= vr.expires_at)
    {
        res.set_success(false);
        res.set_message("TOKEN_EXPIRED");

        infinitepickaxe::Envelope response_env;
        response_env.set_type(infinitepickaxe::HANDSHAKE_RESULT);
        *response_env.mutable_handshake_result() = res;
        send_envelope(response_env);
        close();
        return;
    }
    if (!game_repo_.ensure_user_initialized(vr.user_id))
    {
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

    if (registry_)
    {
        if (auto previous = registry_->replace_session(user_id_, shared_from_this()))
        {
            previous->notify_duplicate_and_close();
        }
    }

    res.set_success(true);
    res.set_message("OK");

    // UserDataSnapshot 구성
    auto *snapshot = res.mutable_snapshot();

    // ?��? 게임 ?�이??조회
    auto game_data = game_repo_.get_user_game_data(user_id_);
    uint32_t cached_mineral_id = 0;
    uint64_t cached_hp = 0;
    uint64_t cached_respawn_until_ms = 0;
    bool has_cached_mineral = load_cached_mining_state(cached_mineral_id, cached_hp, cached_respawn_until_ms);
    std::optional<uint32_t> current_mineral_id = game_data.current_mineral_id;
    std::optional<uint64_t> current_mineral_hp = game_data.current_mineral_hp;
    if (has_cached_mineral && cached_mineral_id > 0) {
        current_mineral_id = cached_mineral_id;
        current_mineral_hp = cached_hp;
    }
    snapshot->mutable_gold()->set_value(game_data.gold);
    snapshot->mutable_crystal()->set_value(game_data.crystal);

    // ?�롯 ?�금 ?�태
    for (bool unlocked : game_data.unlocked_slots)
    {
        snapshot->add_unlocked_slots(unlocked);
    }

    // ?�재 채굴 중인 광물 ?�보 (DB?�서 조회, nullable 처리)
    if (current_mineral_id.has_value() && current_mineral_id.value() > 0)
    {
        const auto *mineral = metadata_.mineral(current_mineral_id.value());
        snapshot->mutable_current_mineral_id()->set_value(current_mineral_id.value());
        snapshot->mutable_mineral_hp()->set_value(current_mineral_hp.value_or(0));
        snapshot->mutable_mineral_max_hp()->set_value(mineral ? mineral->hp : 100);
    }

    // ?�롯 ?�보 �?�?DPS
    auto slots_response = slot_service_.handle_all_slots(user_id_);
    for (const auto &slot : slots_response.slots())
    {
        *snapshot->add_pickaxe_slots() = slot;
    }
    snapshot->set_total_dps(slots_response.total_dps());

    // ?�버 ?�간
    snapshot->mutable_server_time()->set_value(
        static_cast<uint64_t>(
            std::chrono::duration_cast<std::chrono::milliseconds>(
                std::chrono::system_clock::now().time_since_epoch())
                .count()));

    // 광고 카운??추�?
    auto ad_counters = mission_service_.get_ad_counters(user_id_);
    for (const auto &counter : ad_counters)
    {
        auto *ad_counter = snapshot->add_ad_counters();
        ad_counter->set_ad_type(counter.ad_type);
        ad_counter->set_ad_count(counter.ad_count);
        uint32_t limit = 0;
        if (const auto *ad_meta = metadata_.ad_meta(counter.ad_type))
        {
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

    auto missions_res = mission_service_.get_missions(user_id_);
    infinitepickaxe::Envelope missions_env;
    missions_env.set_type(infinitepickaxe::DAILY_MISSIONS_RESPONSE);
    *missions_env.mutable_daily_missions_response() = missions_res;
    send_envelope(missions_env);

    // 채굴 ?��??�이???�작 (DB?�서 로드???�재 광물�? nullable 처리)
    
    if (current_mineral_id.has_value() && current_mineral_id.value() > 0 && current_mineral_hp.has_value())
    {
        mining_state_.current_mineral_id = current_mineral_id.value();
        const auto *current_mineral = metadata_.mineral(mining_state_.current_mineral_id);
        mining_state_.max_hp = current_mineral ? current_mineral->hp : 0;

        uint64_t hp = current_mineral_hp.value();
        if (mining_state_.max_hp > 0 && hp > mining_state_.max_hp)
        {
            hp = mining_state_.max_hp;
        }
        mining_state_.current_hp = hp;

        const uint64_t now_ms = static_cast<uint64_t>(
            std::chrono::duration_cast<std::chrono::milliseconds>(
                std::chrono::system_clock::now().time_since_epoch())
                .count());

        if (cached_respawn_until_ms > now_ms)
        {
            mining_state_.respawn_timer_ms =
                static_cast<float>(cached_respawn_until_ms - now_ms);
            mining_state_.is_mining = false;
        }
        else if (mining_state_.current_hp > 0 && mining_state_.max_hp > 0)
        {
            mining_state_.respawn_timer_ms = 0.0f;
            mining_state_.is_mining = true;
            mining_state_.last_sent_hp = std::numeric_limits<uint64_t>::max();
            refresh_slots_from_service(false);
            send_mining_update({});
            mining_state_.last_sent_hp = mining_state_.current_hp;
        }
        else
        {
            if (current_mineral && mining_state_.max_hp > 0)
            {
                mining_state_.respawn_timer_ms =
                    static_cast<float>(current_mineral->respawn_time) * 1000.0f;
            }
            else
            {
                mining_state_.respawn_timer_ms = 0.0f;
            }
            mining_state_.is_mining = false;
        }
    }
    else
    {
        // ??? ???? ?? ?? ?? ?? ??? ???
        mining_state_.current_mineral_id = 0;
        mining_state_.current_hp = 0;
        mining_state_.max_hp = 0;
        mining_state_.respawn_timer_ms = 0.0f;
        mining_state_.is_mining = false;
    }

    read_length();
}

void Session::handle_heartbeat(const infinitepickaxe::Envelope &env)
{
    if (!env.has_heartbeat())
    {
        send_error("2004", "heartbeat message missing");
        return;
    }

    infinitepickaxe::HeartbeatAck ack;
    ack.set_server_time_ms(
        static_cast<uint64_t>(
            std::chrono::duration_cast<std::chrono::milliseconds>(
                std::chrono::system_clock::now().time_since_epoch())
                .count()));

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::HEARTBEAT_ACK);
    *response_env.mutable_heartbeat_ack() = ack;
    send_envelope(response_env);
}

void Session::handle_upgrade(const infinitepickaxe::Envelope &env)
{
    if (!env.has_upgrade_request())
    {
        send_error("2004", "upgrade_request message missing");
        return;
    }
    const auto &req = env.upgrade_request();
    // ?�재 ?�롯 ?�벨??조회??target_level = current + 1 �??�정
    auto slot = slot_service_.get_slot(user_id_, req.slot_index());
    infinitepickaxe::UpgradeResult res;
    if (!slot.has_value())
    {
        res.set_success(false);
        res.set_slot_index(req.slot_index());
        res.set_error_code("3004"); // SLOT_NOT_FOUND
    }
    else
    {
        uint32_t target_level = slot->level + 1;
        res = upgrade_service_.handle_upgrade(user_id_, req.slot_index(), target_level);
    }

    if (res.success() && mining_state_.is_mining)
    {
        float new_attack_speed = static_cast<float>(res.new_attack_speed_x100()) / 100.0f;
        apply_slot_update(res.slot_index(), res.new_attack_power(), new_attack_speed,
                          res.new_critical_hit_percent(), res.new_critical_damage());
    }

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::UPGRADE_RESULT);
    *response_env.mutable_upgrade_result() = res;
    send_envelope(response_env);

    if (slot.has_value()) {
        auto updates = mission_service_.handle_upgrade_try(user_id_, res.success());
        send_mission_progress_updates(updates);
    }
}

void Session::handle_change_mineral(const infinitepickaxe::Envelope &env)
{
    if (!env.has_change_mineral_request())
    {
        send_error("2004", "change_mineral_request message missing");
        return;
    }
    const auto &req = env.change_mineral_request();

    infinitepickaxe::ChangeMineralResponse res;
    res.set_success(false);
    res.set_error_code("");

    uint32_t mineral_id = req.mineral_id();
    uint64_t hp = 0;

    if (mineral_id == 0)
    {
        // ?? ??
        hp = 0;
    }
    else
    {
        const auto *mineral = metadata_.mineral(mineral_id);
        if (!mineral)
        {
            res.set_error_code("INVALID_MINERAL");
        }
        else
        {
            hp = mineral->hp;
        }
    }

    if (res.error_code().empty())
    {
        if (!game_repo_.set_current_mineral(user_id_, mineral_id, hp))
        {
            res.set_error_code("DB_ERROR");
        }
        else
        {
            const bool needs_delay = (mineral_id != 0); // 광물 선택 시 항상 5초 대기 후 시작

            mining_state_.current_mineral_id = mineral_id;
            mining_state_.current_hp = hp;
            mining_state_.max_hp = hp;
            mining_state_.is_mining = false;
            mining_state_.respawn_timer_ms = needs_delay ? std::max(mining_state_.respawn_timer_ms, 5000.0f) : 0.0f; // 변경 시 5초 대기 후 시작
            if (!needs_delay && mineral_id != 0)
            {
                start_new_mineral();
            }
            cache_mining_state();

            res.set_success(true);
            res.set_mineral_id(mineral_id);
            res.set_mineral_hp(hp);
            res.set_mineral_max_hp(hp);
        }
    }

    if (!res.success() && res.error_code().empty())
    {
        res.set_error_code("UNKNOWN_ERROR");
    }

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::CHANGE_MINERAL_RESPONSE);
    *response_env.mutable_change_mineral_response() = res;
    send_envelope(response_env);
}

void Session::handle_mission(const infinitepickaxe::Envelope &env)
{
    auto res = mission_service_.get_missions(user_id_);

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::DAILY_MISSIONS_RESPONSE);
    *response_env.mutable_daily_missions_response() = res;
    send_envelope(response_env);
}

void Session::handle_mission_progress_update(const infinitepickaxe::Envelope &env)
{
    if (!env.has_mission_progress_update())
    {
        send_error("2004", "mission_progress_update message missing");
        return;
    }
    spdlog::debug("Ignoring client mission_progress_update (server-authoritative): user={}", user_id_);
}

void Session::handle_mission_complete(const infinitepickaxe::Envelope &env)
{
    if (!env.has_mission_complete())
    {
        send_error("2004", "mission_complete message missing");
        return;
    }
    const auto &req = env.mission_complete();
    auto res = mission_service_.claim_mission_reward(user_id_, req.slot_no());

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::MISSION_COMPLETE_RESULT);
    *response_env.mutable_mission_complete_result() = res;
    send_envelope(response_env);
}

void Session::handle_mission_reroll(const infinitepickaxe::Envelope &env)
{
    auto res = mission_service_.reroll_missions(user_id_);

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::MISSION_REROLL_RESULT);
    *response_env.mutable_mission_reroll_result() = res;
    send_envelope(response_env);
}

void Session::handle_ad_watch(const infinitepickaxe::Envelope &env)
{
    if (!env.has_ad_watch_complete())
    {
        send_error("2004", "ad_watch_complete message missing");
        return;
    }
    const auto &req = env.ad_watch_complete();
    auto res = mission_service_.handle_ad_watch(user_id_, req.ad_type());

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::AD_WATCH_RESULT);
    *response_env.mutable_ad_watch_result() = res;
    send_envelope(response_env);
}

void Session::handle_milestone_claim(const infinitepickaxe::Envelope &env)
{
    if (!env.has_milestone_claim())
    {
        send_error("2004", "milestone_claim message missing");
        return;
    }

    const auto &req = env.milestone_claim();
    auto res = mission_service_.handle_milestone_claim(user_id_, req.milestone_count());

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::MILESTONE_CLAIM_RESULT);
    *response_env.mutable_milestone_claim_result() = res;
    send_envelope(response_env);
}

void Session::handle_slot_unlock(const infinitepickaxe::Envelope &env)
{
    if (!env.has_slot_unlock())
    {
        send_error("2004", "slot_unlock message missing");
        return;
    }
    const auto &req = env.slot_unlock();
    auto res = slot_service_.handle_unlock(user_id_, req.slot_index());

    if (res.success() && mining_state_.is_mining)
    {
        refresh_slots_from_service(true);
    }

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::SLOT_UNLOCK_RESULT);
    *response_env.mutable_slot_unlock_result() = res;
    send_envelope(response_env);
}

void Session::handle_all_slots(const infinitepickaxe::Envelope &env)
{
    auto res = slot_service_.handle_all_slots(user_id_);

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::ALL_SLOTS_RESPONSE);
    *response_env.mutable_all_slots_response() = res;
    send_envelope(response_env);
}

void Session::handle_offline_reward(const infinitepickaxe::Envelope &env)
{
    if (!env.has_offline_reward_request())
    {
        send_error("2004", "offline_reward_request message missing");
        return;
    }
    auto res = offline_service_.handle_request(user_id_);

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::OFFLINE_REWARD_RESULT);
    *response_env.mutable_offline_reward_result() = res;
    send_envelope(response_env);
}

void Session::init_router()
{
    router_.register_handler(infinitepickaxe::HEARTBEAT, [this](const infinitepickaxe::Envelope &e)
                             { handle_heartbeat(e); });
    // router_.register_handler(infinitepickaxe::MINING_START, [this](const infinitepickaxe::Envelope& e) { handle_mining(e); });
    // router_.register_handler(infinitepickaxe::MINING_SYNC, [this](const infinitepickaxe::Envelope& e) { handle_mining(e); });
    router_.register_handler(infinitepickaxe::UPGRADE_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_upgrade(e); });
    router_.register_handler(infinitepickaxe::CHANGE_MINERAL_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_change_mineral(e); });
    router_.register_handler(infinitepickaxe::DAILY_MISSIONS_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_mission(e); });
    router_.register_handler(infinitepickaxe::MISSION_PROGRESS_UPDATE, [this](const infinitepickaxe::Envelope &e)
                             { handle_mission_progress_update(e); });
    router_.register_handler(infinitepickaxe::MISSION_COMPLETE, [this](const infinitepickaxe::Envelope &e)
                             { handle_mission_complete(e); });
    router_.register_handler(infinitepickaxe::MISSION_REROLL, [this](const infinitepickaxe::Envelope &e)
                             { handle_mission_reroll(e); });
    router_.register_handler(infinitepickaxe::AD_WATCH_COMPLETE, [this](const infinitepickaxe::Envelope &e)
                             { handle_ad_watch(e); });
    router_.register_handler(infinitepickaxe::MILESTONE_CLAIM, [this](const infinitepickaxe::Envelope &e)
                             { handle_milestone_claim(e); });
    router_.register_handler(infinitepickaxe::SLOT_UNLOCK, [this](const infinitepickaxe::Envelope &e)
                             { handle_slot_unlock(e); });
    router_.register_handler(infinitepickaxe::ALL_SLOTS_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_all_slots(e); });
    router_.register_handler(infinitepickaxe::OFFLINE_REWARD_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_offline_reward(e); });
}

void Session::send_envelope(const infinitepickaxe::Envelope &env)
{
    std::string body;
    env.SerializeToString(&body);
    auto len = static_cast<uint32_t>(body.size());
    auto len_enc = encode_le(len);

    auto self = shared_from_this();
    std::array<boost::asio::const_buffer, 2> bufs = {
        boost::asio::buffer(len_enc),
        boost::asio::buffer(body)};
    boost::asio::async_write(socket_, bufs,
                             [this, self](boost::system::error_code ec, std::size_t /*written*/)
                             {
                                 if (ec)
                                 {
                                     close();
                                     return;
                                 }
                             });
}

void Session::send_error(const std::string &code, const std::string &message)
{
    infinitepickaxe::ErrorNotification err;
    err.set_error_code(code);
    err.set_message(message);

    infinitepickaxe::Envelope env;
    env.set_type(infinitepickaxe::ERROR_NOTIFICATION);
    *env.mutable_error_notification() = err;
    send_envelope(env);
}

void Session::send_mission_progress_updates(const std::vector<infinitepickaxe::MissionProgressUpdate>& updates)
{
    for (const auto& update : updates)
    {
        infinitepickaxe::Envelope env;
        env.set_type(infinitepickaxe::MISSION_PROGRESS_UPDATE);
        *env.mutable_mission_progress_update() = update;
        send_envelope(env);
    }
}

void Session::cache_mining_state()
{
    if (!authenticated_ || user_id_.empty())
    {
        return;
    }

    const uint64_t now_ms = static_cast<uint64_t>(
        std::chrono::duration_cast<std::chrono::milliseconds>(
            std::chrono::system_clock::now().time_since_epoch()).count());
    uint64_t respawn_until_ms = 0;
    if (mining_state_.respawn_timer_ms > 0.0f)
    {
        respawn_until_ms = now_ms + static_cast<uint64_t>(mining_state_.respawn_timer_ms);
    }

    std::unordered_map<std::string, std::string> fields{
        {"mineral_id", std::to_string(mining_state_.current_mineral_id)},
        {"current_hp", std::to_string(mining_state_.current_hp)},
        {"max_hp", std::to_string(mining_state_.max_hp)},
        {"respawn_until_ms", std::to_string(respawn_until_ms)},
        {"updated_at", std::to_string(static_cast<uint64_t>(
            std::chrono::duration_cast<std::chrono::seconds>(
                std::chrono::system_clock::now().time_since_epoch()).count()))}
    };
    const std::string key = "session:mining:" + user_id_;
    redis_.hset_fields(key, fields, std::chrono::seconds(kMiningCacheTtlSeconds));
}

bool Session::load_cached_mining_state(uint32_t& mineral_id, uint64_t& hp, uint64_t& respawn_until_ms)
{
    if (user_id_.empty())
    {
        return false;
    }

    std::unordered_map<std::string, std::string> fields;
    const std::string key = "session:mining:" + user_id_;
    if (!redis_.hgetall(key, fields) || fields.empty())
    {
        return false;
    }

    uint32_t cached_mineral = 0;
    uint64_t cached_hp = 0;
    if (!parse_u32(fields["mineral_id"], cached_mineral))
    {
        return false;
    }
    if (!parse_u64(fields["current_hp"], cached_hp))
    {
        return false;
    }

    mineral_id = cached_mineral;
    hp = cached_hp;
    respawn_until_ms = 0;
    auto it = fields.find("respawn_until_ms");
    if (it != fields.end())
    {
        uint64_t cached_respawn = 0;
        if (parse_u64(it->second, cached_respawn))
        {
            respawn_until_ms = cached_respawn;
        }
    }
    return true;
}

void Session::flush_play_time_progress(bool force)
{
    if (!authenticated_ || user_id_.empty())
    {
        return;
    }

    uint32_t seconds = static_cast<uint32_t>(play_time_accum_ms_ / 1000.0f);
    if (seconds == 0)
    {
        return;
    }

    uint32_t flush_seconds = seconds;
    if (!force)
    {
        flush_seconds = (seconds / kPlayTimeFlushSeconds) * kPlayTimeFlushSeconds;
    }

    if (flush_seconds == 0)
    {
        return;
    }

    play_time_accum_ms_ -= static_cast<float>(flush_seconds * 1000);
    auto updates = mission_service_.handle_play_time_seconds(user_id_, flush_seconds);
    send_mission_progress_updates(updates);
}

void Session::close()
{
    if (closed_)
        return;
    flush_play_time_progress(true);
    closed_ = true;
    boost::system::error_code timer_ec;
    auth_timer_.cancel(timer_ec);
    if (registry_ && !user_id_.empty())
    {
        registry_->remove_if_match(user_id_, this);
    }
    boost::system::error_code ignored;
    socket_.shutdown(boost::asio::ip::tcp::socket::shutdown_both, ignored);
    socket_.close(ignored);
}

void Session::start_auth_timer()
{
    auto self = shared_from_this();
    auth_timer_.expires_after(std::chrono::seconds(5));
    auth_timer_.async_wait([this, self](const boost::system::error_code &ec)
                           {
                               if (ec)
                                   return; // cancelled
                               if (!authenticated_)
                               {
                                   send_error("1007", "AUTH_TIMEOUT");
                                   close();
                               } });
}

void Session::update_mining_tick(float delta_ms)
{
    // 인증되지 않았거나 세션이 닫혔으면 무시
    if (!authenticated_ || closed_)
    {
        return;
    }

    play_time_accum_ms_ += delta_ms;
    flush_play_time_progress(false);
    mining_cache_accum_ms_ += delta_ms;
    if (mining_cache_accum_ms_ >= static_cast<float>(kMiningCacheFlushSeconds) * 1000.0f)
    {
        cache_mining_state();
        mining_cache_accum_ms_ = 0.0f;
    }

    // 광물 선택이 0(중단)이면 채굴 자동 틱 중지
    if (mining_state_.current_mineral_id == 0)
    {
        mining_state_.is_mining = false;
        mining_state_.respawn_timer_ms = 0;
        return;
    }

    // 채굴 중이 아니면 리스폰 타이머 처리
    if (!mining_state_.is_mining)
    {
        if (mining_state_.respawn_timer_ms > 0)
        {
            mining_state_.respawn_timer_ms -= delta_ms;
            if (mining_state_.respawn_timer_ms <= 0)
            {
                start_new_mineral();
            }
        }
        return;
    }

    std::vector<infinitepickaxe::PickaxeAttack> attacks;
    uint64_t total_damage = 0;

    for (auto &slot : mining_state_.slots)
    {
        slot.next_attack_timer_ms -= delta_ms;

        // 40ms 동안 여러 번 공격할 수 있음 (attack_speed가 매우 빠른 경우)
        while (slot.next_attack_timer_ms <= 0)
        {
            const float attack_speed = std::max(slot.attack_speed, 0.01f);
            const float attack_interval_ms = 1000.0f / attack_speed;

            const bool is_crit = roll_bp_10000() < slot.critical_hit_percent;
            uint64_t damage = slot.attack_power;
            if (is_crit)
            {
                damage = static_cast<uint64_t>(
                    (static_cast<long double>(slot.attack_power) * static_cast<long double>(slot.critical_damage)) / 10000.0L);
            }

            infinitepickaxe::PickaxeAttack attack;
            attack.set_slot_index(slot.slot_index);
            attack.set_damage(damage);
            attack.set_is_critical(is_crit);
            attacks.push_back(attack);

            slot.next_attack_timer_ms += attack_interval_ms;

            total_damage += damage;
        }
    }

    if (total_damage > 0)
    {
        if (mining_state_.current_hp > total_damage)
        {
            mining_state_.current_hp -= total_damage;
        }
        else
        {
            mining_state_.current_hp = 0;
        }
    }

    if (mining_state_.current_hp == 0)
    {
        // 마지막 타격 결과를 클라이언트에 반영 후 완료 통보
        send_mining_update(attacks);
        mining_state_.last_sent_hp = mining_state_.current_hp;
        handle_mining_complete_immediate();
        return;
    }

    if (mining_state_.current_hp != mining_state_.last_sent_hp)
    {
        send_mining_update(attacks);
        mining_state_.last_sent_hp = mining_state_.current_hp;
    }
}

void Session::start_new_mineral()
{
    // 새 광물로 시작
    if (mining_state_.current_mineral_id == 0)
    {
        // 채굴 중단 상태
        mining_state_.is_mining = false;
        mining_state_.respawn_timer_ms = 0;
        return;
    }

    const auto *mineral = metadata_.mineral(mining_state_.current_mineral_id);
    if (!mineral)
    {
        spdlog::error("Invalid mineral_id: {}", mining_state_.current_mineral_id);
        mining_state_.is_mining = false;
        return;
    }

    mining_state_.current_hp = mineral->hp;
    mining_state_.max_hp = mineral->hp;
    mining_state_.is_mining = true;
    mining_state_.respawn_timer_ms = 0;
    mining_state_.last_sent_hp = std::numeric_limits<uint64_t>::max();

    refresh_slots_from_service(false);

    // 초기 상태를 클라이언트에 전달 (HP 변화 알림)
    send_mining_update({});
    mining_state_.last_sent_hp = mining_state_.current_hp;

    spdlog::info("Mining started: user={} mineral={} hp={} slots={}",
                 user_id_, mining_state_.current_mineral_id, mining_state_.current_hp, mining_state_.slots.size());
}

void Session::refresh_slots_from_service(bool preserve_timers)
{
    std::unordered_map<uint32_t, float> previous_timers;
    if (preserve_timers)
    {
        for (const auto &slot : mining_state_.slots)
        {
            previous_timers[slot.slot_index] = slot.next_attack_timer_ms;
        }
    }

    auto slots_response = slot_service_.handle_all_slots(user_id_);
    mining_state_.slots.clear();

    for (const auto &slot_info : slots_response.slots())
    {
        if (!slot_info.is_unlocked())
        {
            continue;
        }

        SlotMiningState slot{};
        slot.slot_index = slot_info.slot_index();
        slot.attack_power = slot_info.attack_power();
        slot.attack_speed = static_cast<float>(slot_info.attack_speed_x100()) / 100.0f;
        if (slot.attack_speed <= 0.0f)
        {
            slot.attack_speed = 0.01f;
        }
        slot.critical_hit_percent = slot_info.critical_hit_percent();
        slot.critical_damage = slot_info.critical_damage();

        float attack_interval_ms = 1000.0f / slot.attack_speed;
        auto it = previous_timers.find(slot.slot_index);
        if (preserve_timers && it != previous_timers.end())
        {
            slot.next_attack_timer_ms = std::clamp(it->second, 1.0f, attack_interval_ms);
        }
        else
        {
            slot.next_attack_timer_ms = (float)(std::rand() % 1000) / 1000.0f * attack_interval_ms;
        }
        mining_state_.slots.push_back(slot);
    }
}

void Session::apply_slot_update(uint32_t slot_index, uint64_t attack_power, float attack_speed,
                                uint32_t critical_hit_percent, uint32_t critical_damage)
{
    if (attack_speed <= 0.0f)
    {
        attack_speed = 0.01f;
    }

    auto it = std::find_if(mining_state_.slots.begin(), mining_state_.slots.end(),
                           [slot_index](const SlotMiningState &s)
                           { return s.slot_index == slot_index; });

    if (it == mining_state_.slots.end())
    {
        if (!mining_state_.is_mining)
        {
            return;
        }
        SlotMiningState slot{};
        slot.slot_index = slot_index;
        slot.attack_power = attack_power;
        slot.attack_speed = attack_speed;
        slot.critical_hit_percent = critical_hit_percent;
        slot.critical_damage = critical_damage;
        float attack_interval_ms = 1000.0f / slot.attack_speed;
        slot.next_attack_timer_ms = (float)(std::rand() % 1000) / 1000.0f * attack_interval_ms;
        mining_state_.slots.push_back(slot);
        return;
    }

    it->attack_power = attack_power;
    it->attack_speed = attack_speed;
    it->critical_hit_percent = critical_hit_percent;
    it->critical_damage = critical_damage;

    float attack_interval_ms = 1000.0f / it->attack_speed;
    it->next_attack_timer_ms = std::clamp(it->next_attack_timer_ms, 1.0f, attack_interval_ms);
}

void Session::send_mining_update(const std::vector<infinitepickaxe::PickaxeAttack> &attacks)
{
    infinitepickaxe::MiningUpdate update;
    update.set_mineral_id(mining_state_.current_mineral_id);
    update.set_current_hp(mining_state_.current_hp);
    update.set_max_hp(mining_state_.max_hp);
    update.set_server_timestamp(static_cast<uint64_t>(
        std::chrono::duration_cast<std::chrono::milliseconds>(
            std::chrono::system_clock::now().time_since_epoch())
            .count()));

    for (const auto &attack : attacks)
    {
        *update.add_attacks() = attack;
    }

    infinitepickaxe::Envelope env;
    env.set_type(infinitepickaxe::MINING_UPDATE);
    *env.mutable_mining_update() = update;
    send_envelope(env);
}

void Session::handle_mining_complete_immediate()
{
    mining_state_.is_mining = false;

    const auto *mineral = metadata_.mineral(mining_state_.current_mineral_id);
    if (!mineral)
    {
        spdlog::error("Invalid mineral_id: {}", mining_state_.current_mineral_id);
        return;
    }

    uint64_t gold_reward = mineral->reward;
    uint32_t respawn_time_sec = mineral->respawn_time;

    auto completion_result = mining_service_.handle_complete(user_id_, mining_state_.current_mineral_id);
    mining_state_.respawn_timer_ms = respawn_time_sec * 1000.0f;
    mining_state_.last_sent_hp = mining_state_.current_hp;

    infinitepickaxe::MiningComplete complete;
    complete.set_mineral_id(mining_state_.current_mineral_id);
    complete.set_gold_earned(completion_result.gold_earned());
    complete.set_total_gold(completion_result.total_gold());
    complete.set_mining_count(completion_result.mining_count());
    complete.set_respawn_time(respawn_time_sec);
    complete.set_server_timestamp(static_cast<uint64_t>(
        std::chrono::duration_cast<std::chrono::milliseconds>(
            std::chrono::system_clock::now().time_since_epoch())
            .count()));

    infinitepickaxe::Envelope env;
    env.set_type(infinitepickaxe::MINING_COMPLETE);
    *env.mutable_mining_complete() = complete;
    send_envelope(env);

    auto updates = mission_service_.handle_mining_complete(user_id_, mining_state_.current_mineral_id);
    auto gold_updates = mission_service_.handle_gold_earned(user_id_, completion_result.gold_earned());
    updates.insert(updates.end(), gold_updates.begin(), gold_updates.end());
    send_mission_progress_updates(updates);
    cache_mining_state();

    spdlog::info("Mining completed: user={} mineral={} gold_earned={} respawn_time={}s",
                 user_id_, mining_state_.current_mineral_id, gold_reward, respawn_time_sec);
}
