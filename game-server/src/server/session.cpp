#include "session.h"
#include "metadata/metadata_loader.h"
#include "ad_service.h"
#include "infinite_mine_service.h"
#include "time_utils.h"
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

    constexpr int kOfflineSessionTtlSeconds = 60 * 60 * 24 * 90;

    struct OfflineSessionData
    {
        uint64_t start_ms{0};
        uint32_t available_seconds{0};
        uint32_t mineral_id{0};
        uint64_t current_hp{0};
        uint64_t respawn_remaining_ms{0};
        uint64_t total_dps{0};
    };

    struct OfflineMiningResult
    {
        uint64_t gold_earned{0};
        uint32_t mining_count{0};
        uint64_t remaining_hp{0};
        uint64_t respawn_remaining_ms{0};
    };

    uint64_t now_ms_utc()
    {
        return static_cast<uint64_t>(
            std::chrono::duration_cast<std::chrono::milliseconds>(
                std::chrono::system_clock::now().time_since_epoch())
                .count());
    }

    uint32_t kst_date_yyyymmdd(uint64_t epoch_ms)
    {
        auto tp = std::chrono::system_clock::time_point(std::chrono::milliseconds(epoch_ms));
        auto kst_tp = tp + std::chrono::hours(9);
        std::time_t tt = std::chrono::system_clock::to_time_t(kst_tp);
        std::tm tm = *std::gmtime(&tt);
        return static_cast<uint32_t>(
            (tm.tm_year + 1900) * 10000 +
            (tm.tm_mon + 1) * 100 +
            tm.tm_mday);
    }

    bool parse_offline_session(const std::unordered_map<std::string, std::string>& fields, OfflineSessionData& out)
    {
        auto it_start = fields.find("start_ms");
        auto it_available = fields.find("available_seconds");
        auto it_mineral = fields.find("mineral_id");
        auto it_hp = fields.find("current_hp");
        auto it_respawn = fields.find("respawn_remaining_ms");
        auto it_dps = fields.find("total_dps");

        if (it_start == fields.end() || it_available == fields.end() ||
            it_mineral == fields.end() || it_hp == fields.end() ||
            it_respawn == fields.end() || it_dps == fields.end())
        {
            return false;
        }

        if (!parse_u64(it_start->second, out.start_ms)) return false;
        if (!parse_u32(it_available->second, out.available_seconds)) return false;
        if (!parse_u32(it_mineral->second, out.mineral_id)) return false;
        if (!parse_u64(it_hp->second, out.current_hp)) return false;
        if (!parse_u64(it_respawn->second, out.respawn_remaining_ms)) return false;
        if (!parse_u64(it_dps->second, out.total_dps)) return false;

        return true;
    }

    OfflineMiningResult simulate_offline_mining(const MineralMeta& mineral,
                                                uint64_t total_dps,
                                                uint64_t current_hp,
                                                uint64_t respawn_remaining_ms,
                                                uint64_t elapsed_seconds)
    {
        OfflineMiningResult result;
        if (total_dps == 0 || elapsed_seconds == 0 || mineral.hp == 0)
        {
            result.remaining_hp = current_hp;
            result.respawn_remaining_ms = respawn_remaining_ms;
            return result;
        }

        long double elapsed = static_cast<long double>(elapsed_seconds);
        long double dps = static_cast<long double>(total_dps);
        long double max_hp = static_cast<long double>(mineral.hp);
        long double hp = static_cast<long double>(current_hp);
        if (hp < 0.0L) hp = 0.0L;
        if (hp > max_hp) hp = max_hp;

        long double respawn_remaining = static_cast<long double>(respawn_remaining_ms) / 1000.0L;
        long double respawn_time = static_cast<long double>(mineral.respawn_time);

        if (respawn_remaining > 0.0L)
        {
            if (elapsed < respawn_remaining)
            {
                respawn_remaining -= elapsed;
                elapsed = 0.0L;
            }
            else
            {
                elapsed -= respawn_remaining;
                respawn_remaining = 0.0L;
                hp = max_hp;
            }
        }

        if (elapsed <= 0.0L)
        {
            if (respawn_remaining > 0.0L)
            {
                result.remaining_hp = 0;
                result.respawn_remaining_ms = static_cast<uint64_t>(std::llround(std::max(0.0L, respawn_remaining) * 1000.0L));
            }
            else
            {
                result.remaining_hp = static_cast<uint64_t>(std::llround(std::max(0.0L, hp)));
                result.respawn_remaining_ms = 0;
            }
            return result;
        }

        if (hp <= 0.0L)
        {
            hp = max_hp;
        }

        long double time_to_kill = hp / dps;
        if (elapsed < time_to_kill)
        {
            hp -= dps * elapsed;
            result.remaining_hp = static_cast<uint64_t>(std::llround(std::max(0.0L, hp)));
            result.respawn_remaining_ms = 0;
            return result;
        }

        elapsed -= time_to_kill;
        result.mining_count += 1;
        result.gold_earned += mineral.reward;

        long double full_kill_time = max_hp / dps;
        long double cycle_time = full_kill_time + respawn_time;
        if (cycle_time > 0.0L && elapsed >= cycle_time)
        {
            auto cycles = static_cast<unsigned long long>(std::floor(elapsed / cycle_time));
            if (cycles > 0)
            {
                result.mining_count += static_cast<uint32_t>(std::min<unsigned long long>(cycles, std::numeric_limits<uint32_t>::max() - result.mining_count));
                result.gold_earned += static_cast<uint64_t>(cycles) * mineral.reward;
                elapsed -= static_cast<long double>(cycles) * cycle_time;
            }
        }

        if (respawn_time > 0.0L)
        {
            if (elapsed < respawn_time)
            {
                respawn_remaining = respawn_time - elapsed;
                result.remaining_hp = 0;
                result.respawn_remaining_ms = static_cast<uint64_t>(std::llround(std::max(0.0L, respawn_remaining) * 1000.0L));
                return result;
            }
            elapsed -= respawn_time;
        }

        hp = max_hp;
        time_to_kill = hp / dps;
        if (elapsed < time_to_kill)
        {
            hp -= dps * elapsed;
            result.remaining_hp = static_cast<uint64_t>(std::llround(std::max(0.0L, hp)));
            result.respawn_remaining_ms = 0;
            return result;
        }

        elapsed -= time_to_kill;
        result.mining_count += 1;
        result.gold_earned += mineral.reward;

        respawn_remaining = respawn_time - elapsed;
        if (respawn_remaining < 0.0L)
        {
            respawn_remaining = 0.0L;
        }
        result.remaining_hp = 0;
        result.respawn_remaining_ms = static_cast<uint64_t>(std::llround(std::max(0.0L, respawn_remaining) * 1000.0L));

        if (respawn_time == 0.0L && result.remaining_hp == 0)
        {
            result.remaining_hp = mineral.hp;
        }

        return result;
    }
} // namespace

Session::Session(boost::asio::ip::tcp::socket socket,
                 AuthService &auth_service,
                 GameRepository &game_repo,
                 MiningService &mining_service,
                 UpgradeService &upgrade_service,
                 MissionService &mission_service,
                 AchievementService &achievement_service,
                 InfiniteMineService &infinite_mine_service,
                 MailService &mail_service,
                 ItemService &item_service,
                 SlotService &slot_service,
                 OfflineService &offline_service,
                 AdService &ad_service,
                 GemService &gem_service,
                 RedisClient &redis_client,
                 std::shared_ptr<SessionRegistry> registry,
                 const MetadataLoader &metadata)
    : socket_(std::move(socket)),
      strand_(boost::asio::make_strand(static_cast<boost::asio::io_context&>(socket_.get_executor().context()))),
      auth_timer_(strand_),
      auth_service_(auth_service),
      game_repo_(game_repo),
      mining_service_(mining_service),
      upgrade_service_(upgrade_service),
      mission_service_(mission_service),
      achievement_service_(achievement_service),
      infinite_mine_service_(infinite_mine_service),
      mail_service_(mail_service),
      item_service_(item_service),
      slot_service_(slot_service),
      offline_service_(offline_service),
      ad_service_(ad_service),
      gem_service_(gem_service),
      redis_(redis_client),
      registry_(std::move(registry)),
      metadata_(metadata)
{
    init_router();
}

void Session::start()
{
    auto self = shared_from_this();
    boost::asio::dispatch(strand_, [self]()
                           {
                               try
                               {
                                   self->client_ip_ = self->socket_.remote_endpoint().address().to_string();
                               }
                               catch (...)
                               {
                                   self->client_ip_.clear();
                               }
                               self->start_auth_timer();
                               self->read_length();
                           });
}

void Session::notify_duplicate_and_close()
{
    auto self = shared_from_this();
    boost::asio::dispatch(strand_, [self]()
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

                               std::array<boost::asio::const_buffer, 2> bufs = {
                                   boost::asio::buffer(len_enc),
                                   boost::asio::buffer(body)};
                               boost::asio::async_write(self->socket_, bufs,
                                                        boost::asio::bind_executor(
                                                            self->strand_,
                                                            [self](boost::system::error_code /*ec*/, std::size_t /*written*/)
                                                            {
                                                                self->close(false);
                                                            }));
                           });
}

void Session::read_length()
{
    auto self = shared_from_this();
    boost::asio::async_read(socket_, boost::asio::buffer(len_buf_),
                            boost::asio::bind_executor(
                                strand_,
                                [self](boost::system::error_code ec, std::size_t /*len*/)
                                {
                                    if (ec)
                                    {
                                        self->close();
                                        return;
                                    }
                                    uint32_t len = decode_le(self->len_buf_);
                                    if (len == 0 || len > 64 * 1024)
                                    { // 간단한 길이 제한
                                        self->send_error("INVALID_LENGTH", "invalid length");
                                        self->close(false);
                                        return;
                                    }
                                    self->payload_buf_.resize(len);
                                    self->read_payload(len);
                                }));
}

void Session::read_payload(std::size_t length)
{
    auto self = shared_from_this();
    boost::asio::async_read(socket_, boost::asio::buffer(self->payload_buf_.data(), length),
                            boost::asio::bind_executor(
                                strand_,
                                [self](boost::system::error_code ec, std::size_t /*len*/)
                                {
                                    if (ec)
                                    {
                                        self->close();
                                        return;
                                    }
                                    infinitepickaxe::Envelope env;
                                    if (!env.ParseFromArray(self->payload_buf_.data(), static_cast<int>(self->payload_buf_.size())))
                                    {
                                        self->send_error("INVALID_ENVELOPE", "parse failed");
                                        self->close(false);
                                        return;
                                    }
                                    self->dispatch_envelope(env);
                                }));
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
        close(false);
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
        close(false);
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
        close(false);
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
        close(false);
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
        close(false);
        return;
    }
    user_id_ = vr.user_id;
    device_id_ = vr.device_id;
    google_id_ = vr.google_id;
    expires_at_ = vr.expires_at;
    authenticated_ = true;
    boost::system::error_code timer_ec;
    auth_timer_.cancel(timer_ec);
    next_daily_reset_ms_ = kst_next_midnight_ms();
    next_weekly_reset_ms_ = kst_next_week_reset_ms();

    bool resumed = false;
    std::chrono::system_clock::time_point disconnected_at{};
    if (registry_)
    {
        if (auto previous = registry_->replace_session(user_id_, shared_from_this()))
        {
            registry_->clear_grace(user_id_);
            previous->notify_duplicate_and_close();
        }
        else
        {
            resumed = registry_->consume_grace_if_valid(user_id_, device_id_, &disconnected_at);
        }
    }
    if (resumed)
    {
        auto disconnected_ms = std::chrono::duration_cast<std::chrono::milliseconds>(
            disconnected_at.time_since_epoch()).count();
        spdlog::info("Session resumed within grace: user={}, device={}, disconnected_at_ms={}",
                     user_id_, device_id_, disconnected_ms);
    }

    res.set_success(true);
    res.set_message("OK");

    infinitepickaxe::OfflineRewardResult offline_reward;
    bool has_offline_reward = try_consume_offline_session(offline_reward);

    // UserDataSnapshot 구성
    auto *snapshot = res.mutable_snapshot();

    // 유저 게임 데이터 조회
    auto game_data = game_repo_.get_user_game_data(user_id_);
    if (has_offline_reward)
    {
        offline_reward.set_total_gold(game_data.gold);
    }
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

    // 슬롯 해금 상태
    for (bool unlocked : game_data.unlocked_slots)
    {
        snapshot->add_unlocked_slots(unlocked);
    }

    // 현재 채굴 중인 광물 정보 (캐시/DB 기반, nullable 처리)
    if (current_mineral_id.has_value())
    {
        const uint32_t mineral_id = current_mineral_id.value();
        const uint64_t mineral_hp = mineral_id > 0 ? current_mineral_hp.value_or(0) : 0;
        snapshot->mutable_current_mineral_id()->set_value(mineral_id);
        snapshot->mutable_mineral_hp()->set_value(mineral_hp);
        if (mineral_id > 0)
        {
            const auto *mineral = metadata_.mineral(mineral_id);
            snapshot->mutable_mineral_max_hp()->set_value(mineral ? mineral->hp : 100);
        }
        else
        {
            snapshot->mutable_mineral_max_hp()->set_value(0);
        }
    }

    // 슬롯 정보 및 총 DPS
    auto slots_response = slot_service_.handle_all_slots(user_id_);
    for (const auto &slot : slots_response.slots())
    {
        *snapshot->add_pickaxe_slots() = slot;
    }
    snapshot->set_total_dps(slots_response.total_dps());

    // 서버 시간
    snapshot->mutable_server_time()->set_value(
        static_cast<uint64_t>(
            std::chrono::duration_cast<std::chrono::milliseconds>(
                std::chrono::system_clock::now().time_since_epoch())
                .count()));

    auto offline_state = offline_service_.get_state(user_id_);
    snapshot->set_current_offline_seconds(offline_state.current_offline_seconds);

    // 보석 인벤토리 정보
    auto gem_inv = game_repo_.get_gem_inventory_info(user_id_);
    snapshot->set_gem_inventory_capacity(gem_inv.capacity);
    snapshot->set_total_gems(gem_inv.total_gems);

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::HANDSHAKE_RESULT);
    *response_env.mutable_handshake_result() = res;
    send_envelope(response_env);

    send_daily_missions_state();
    send_milestone_state();
    send_weekly_missions_state();
    send_weekly_milestone_state();
    send_achievements_state();
    send_ad_counters_state();
    if (has_offline_reward)
    {
        infinitepickaxe::Envelope reward_env;
        reward_env.set_type(infinitepickaxe::OFFLINE_REWARD_RESULT);
        *reward_env.mutable_offline_reward_result() = offline_reward;
        send_envelope(reward_env);
    }

    // 채굴 상태 초기화 (DB/캐시에서 로드한 현재 광물, nullable 처리)
    
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
        // 현재 채굴 중인 광물이 없는 경우 기본 상태로 초기화
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
    // 현재 슬롯 레벨 조회 후 target_level = current + 1로 설정
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
        float new_attack_speed = static_cast<float>(res.new_attack_speed()) / 10000.0f;
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
        auto weekly_updates = mission_service_.handle_weekly_upgrade_try(user_id_, res.success());
        send_weekly_mission_progress_updates(weekly_updates);
        bool count_upgrade_fail = !res.success() && res.error_code() == "3000";
        auto achievement_updates = achievement_service_.handle_upgrade_try(user_id_, res.success(), count_upgrade_fail);
        send_achievement_progress_updates(achievement_updates);
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
    send_daily_missions_state();
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
    send_daily_missions_state();
    send_milestone_state();
}

void Session::handle_mission_reroll(const infinitepickaxe::Envelope &env)
{
    auto res = mission_service_.reroll_missions(user_id_);

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::MISSION_REROLL_RESULT);
    *response_env.mutable_mission_reroll_result() = res;
    send_envelope(response_env);
    send_daily_missions_state();
}

void Session::handle_weekly_missions(const infinitepickaxe::Envelope &env)
{
    if (!env.has_weekly_missions_request())
    {
        send_error("2004", "weekly_missions_request message missing");
        return;
    }
    send_weekly_missions_state();
    send_weekly_milestone_state();
}

void Session::handle_weekly_mission_progress_update(const infinitepickaxe::Envelope &env)
{
    if (!env.has_weekly_mission_progress_update())
    {
        send_error("2004", "weekly_mission_progress_update message missing");
        return;
    }
    spdlog::debug("Ignoring client weekly_mission_progress_update (server-authoritative): user={}", user_id_);
}

void Session::handle_weekly_mission_claim(const infinitepickaxe::Envelope &env)
{
    if (!env.has_weekly_mission_claim())
    {
        send_error("2004", "weekly_mission_claim message missing");
        return;
    }
    const auto &req = env.weekly_mission_claim();
    auto res = mission_service_.claim_weekly_mission_reward(user_id_, req.mission_id());

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::WEEKLY_MISSION_CLAIM_RESULT);
    *response_env.mutable_weekly_mission_claim_result() = res;
    send_envelope(response_env);
    send_weekly_missions_state();
    send_weekly_milestone_state();
}

void Session::handle_weekly_milestone_claim(const infinitepickaxe::Envelope &env)
{
    if (!env.has_weekly_milestone_claim())
    {
        send_error("2004", "weekly_milestone_claim message missing");
        return;
    }
    const auto &req = env.weekly_milestone_claim();
    auto res = mission_service_.handle_weekly_milestone_claim(user_id_, req.milestone_count());

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::WEEKLY_MILESTONE_CLAIM_RESULT);
    *response_env.mutable_weekly_milestone_claim_result() = res;
    send_envelope(response_env);
    send_weekly_milestone_state();
}

void Session::handle_achievements(const infinitepickaxe::Envelope &env)
{
    if (!env.has_achievements_request())
    {
        send_error("2004", "achievements_request message missing");
        return;
    }
    send_achievements_state();
}

void Session::handle_achievement_progress_update(const infinitepickaxe::Envelope &env)
{
    if (!env.has_achievement_progress_update())
    {
        send_error("2004", "achievement_progress_update message missing");
        return;
    }
    spdlog::debug("Ignoring client achievement_progress_update (server-authoritative): user={}", user_id_);
}

void Session::handle_achievement_claim(const infinitepickaxe::Envelope &env)
{
    if (!env.has_achievement_claim())
    {
        send_error("2004", "achievement_claim message missing");
        return;
    }
    const auto &req = env.achievement_claim();
    auto res = achievement_service_.claim_achievement(user_id_, req.achievement_id());

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::ACHIEVEMENT_CLAIM_RESULT);
    *response_env.mutable_achievement_claim_result() = res;
    send_envelope(response_env);
}

void Session::handle_infinite_mine_state(const infinitepickaxe::Envelope &env)
{
    if (!env.has_infinite_mine_state_request())
    {
        send_error("2004", "infinite_mine_state_request message missing");
        return;
    }
    send_infinite_mine_state();
}

void Session::handle_infinite_mine_challenge_start(const infinitepickaxe::Envelope &env)
{
    if (!env.has_infinite_mine_challenge_start_request())
    {
        send_error("2004", "infinite_mine_challenge_start_request message missing");
        return;
    }

    const auto &req = env.infinite_mine_challenge_start_request();
    infinitepickaxe::InfiniteMineChallengeStartResult res;

    if (infinite_mine_state_.is_challenging)
    {
        res.set_success(false);
        res.set_floor(req.floor());
        res.set_error_code("ALREADY_CHALLENGING");
    }
    else
    {
        res = infinite_mine_service_.start_challenge(user_id_, req.floor());
        if (res.success())
        {
            infinite_mine_state_.is_active = true;
            infinite_mine_state_.is_challenging = true;
            infinite_mine_state_.floor = res.floor();
            infinite_mine_state_.current_hp = res.current_hp();
            infinite_mine_state_.max_hp = res.max_hp();
            infinite_mine_state_.remaining_ms = res.remaining_ms();
            infinite_mine_state_.update_accum_ms = 0.0f;
            build_infinite_mine_slots();
        }
    }

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::INFINITE_MINE_CHALLENGE_START_RESULT);
    *response_env.mutable_infinite_mine_challenge_start_result() = res;
    send_envelope(response_env);
}

void Session::handle_infinite_mine_auto_claim(const infinitepickaxe::Envelope &env)
{
    if (!env.has_infinite_mine_auto_claim_request())
    {
        send_error("2004", "infinite_mine_auto_claim_request message missing");
        return;
    }

    const auto &req = env.infinite_mine_auto_claim_request();
    auto res = infinite_mine_service_.claim_auto_reward(user_id_, req.floor());

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::INFINITE_MINE_AUTO_CLAIM_RESULT);
    *response_env.mutable_infinite_mine_auto_claim_result() = res;
    send_envelope(response_env);

    if (res.success())
    {
        send_infinite_mine_state();
    }
}

void Session::handle_infinite_mine_auto_claim_all(const infinitepickaxe::Envelope &env)
{
    if (!env.has_infinite_mine_auto_claim_all_request())
    {
        send_error("2004", "infinite_mine_auto_claim_all_request message missing");
        return;
    }

    auto res = infinite_mine_service_.claim_all_auto_rewards(user_id_);

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::INFINITE_MINE_AUTO_CLAIM_ALL_RESULT);
    *response_env.mutable_infinite_mine_auto_claim_all_result() = res;
    send_envelope(response_env);

    if (res.success())
    {
        send_infinite_mine_state();
    }
}

void Session::handle_infinite_mine_exit(const infinitepickaxe::Envelope &env)
{
    if (!env.has_infinite_mine_exit_request())
    {
        send_error("2004", "infinite_mine_exit_request message missing");
        return;
    }

    if (infinite_mine_state_.is_challenging)
    {
        end_infinite_mine_challenge(false, infinitepickaxe::CANCELED);
    }

    infinite_mine_state_ = InfiniteMineState{};

    infinitepickaxe::InfiniteMineExitResult res;
    res.set_success(true);
    res.set_error_code("");

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::INFINITE_MINE_EXIT_RESULT);
    *response_env.mutable_infinite_mine_exit_result() = res;
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
    auto res = ad_service_.handle_ad_watch(user_id_, req.ad_type());

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::AD_WATCH_RESULT);
    *response_env.mutable_ad_watch_result() = res;
    send_envelope(response_env);
    send_ad_counters_state();

    if (res.success() && req.ad_type() == "mission_reroll") {
        auto reroll_res = mission_service_.reroll_missions_ad(user_id_);
        infinitepickaxe::Envelope reroll_env;
        reroll_env.set_type(infinitepickaxe::MISSION_REROLL_RESULT);
        *reroll_env.mutable_mission_reroll_result() = reroll_res;
        send_envelope(reroll_env);
        if (reroll_res.success()) {
            send_daily_missions_state();
        }
    }
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
    send_milestone_state();
}

void Session::handle_mail_list(const infinitepickaxe::Envelope &env)
{
    if (!env.has_mail_list_request())
    {
        send_error("2004", "mail_list_request message missing");
        return;
    }

    const auto &req = env.mail_list_request();
    auto res = mail_service_.handle_mail_list(user_id_, req);

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::MAIL_LIST_RESPONSE);
    *response_env.mutable_mail_list_response() = res;
    send_envelope(response_env);
}

void Session::handle_mail_detail(const infinitepickaxe::Envelope &env)
{
    if (!env.has_mail_detail_request())
    {
        send_error("2004", "mail_detail_request message missing");
        return;
    }

    const auto &req = env.mail_detail_request();
    auto res = mail_service_.handle_mail_detail(user_id_, req.mail_id(), req.mark_read());

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::MAIL_DETAIL_RESPONSE);
    *response_env.mutable_mail_detail_response() = res;
    send_envelope(response_env);
}

void Session::handle_mail_claim(const infinitepickaxe::Envelope &env)
{
    if (!env.has_mail_claim_request())
    {
        send_error("2004", "mail_claim_request message missing");
        return;
    }

    const auto &req = env.mail_claim_request();
    auto res = mail_service_.handle_mail_claim(user_id_, req.mail_id());

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::MAIL_CLAIM_RESULT);
    *response_env.mutable_mail_claim_result() = res;
    send_envelope(response_env);
}

void Session::handle_mail_claim_all(const infinitepickaxe::Envelope &env)
{
    if (!env.has_mail_claim_all_request())
    {
        send_error("2004", "mail_claim_all_request message missing");
        return;
    }

    auto res = mail_service_.handle_mail_claim_all(user_id_);

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::MAIL_CLAIM_ALL_RESULT);
    *response_env.mutable_mail_claim_all_result() = res;
    send_envelope(response_env);
}

void Session::handle_item_inventory(const infinitepickaxe::Envelope &env)
{
    if (!authenticated_) {
        send_error("NOT_AUTHENTICATED", "authentication required");
        return;
    }

    if (!env.has_item_inventory_request()) {
        send_error("INVALID_REQUEST", "missing item_inventory_request");
        return;
    }

    auto snapshot = item_service_.handle_inventory(user_id_);

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::ITEM_INVENTORY_RESPONSE);
    auto* res = response_env.mutable_item_inventory_response();
    res->set_current_capacity(snapshot.current_capacity);
    res->set_used_slots(snapshot.used_slots);

    for (const auto& stack : snapshot.stacks) {
        auto* entry = res->add_stacks();
        entry->set_item_id(stack.item_id);
        entry->set_count(stack.count);
    }

    for (const auto& inst : snapshot.instances) {
        auto* entry = res->add_instances();
        entry->set_item_instance_id(inst.item_instance_id);
        entry->set_item_id(inst.item_id);
        entry->set_acquired_at_ms(inst.acquired_at);
    }

    send_envelope(response_env);
}

void Session::handle_item_inventory_expand(const infinitepickaxe::Envelope &env)
{
    if (!authenticated_) {
        send_error("NOT_AUTHENTICATED", "authentication required");
        return;
    }

    if (!env.has_item_inventory_expand_request()) {
        send_error("INVALID_REQUEST", "missing item_inventory_expand_request");
        return;
    }

    auto expand = item_service_.handle_inventory_expand(user_id_);

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::ITEM_INVENTORY_EXPAND_RESULT);
    auto* res = response_env.mutable_item_inventory_expand_result();
    res->set_success(expand.success);
    if (expand.success) {
        res->set_new_capacity(expand.new_capacity);
        res->set_crystal_spent(metadata_.item_inventory_config().expand_cost);
        res->set_remaining_crystal(expand.remaining_crystal);
        res->set_error_code("");
    } else {
        if (expand.max_capacity_reached) {
            res->set_error_code("MAX_CAPACITY");
        } else if (expand.insufficient_crystal) {
            res->set_error_code("INSUFFICIENT_CRYSTAL");
        } else {
            res->set_error_code("DB_ERROR");
        }
    }

    send_envelope(response_env);
}

void Session::handle_use_item(const infinitepickaxe::Envelope &env)
{
    if (!authenticated_) {
        send_error("NOT_AUTHENTICATED", "authentication required");
        return;
    }

    if (!env.has_use_item_request()) {
        send_error("INVALID_REQUEST", "missing use_item_request");
        return;
    }

    const auto& req = env.use_item_request();
    infinitepickaxe::UseItemResult res;
    res.set_item_id(req.item_id());
    res.set_count_used(0);
    res.set_success(false);
    res.set_error_code("NOT_IMPLEMENTED");

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::USE_ITEM_RESULT);
    *response_env.mutable_use_item_result() = res;
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

void Session::handle_offline_mode_start(const infinitepickaxe::Envelope &env)
{
    if (!env.has_offline_mode_start_request())
    {
        send_error("2004", "offline_mode_start_request message missing");
        return;
    }

    infinitepickaxe::OfflineModeStartResult res;
    res.set_success(false);
    res.set_error_code("");

    auto offline_state = offline_service_.get_state(user_id_);
    res.set_current_offline_seconds(offline_state.current_offline_seconds);

    if (offline_state.current_offline_seconds == 0)
    {
        res.set_error_code("NO_OFFLINE_TIME");
    }

    uint32_t mineral_id = mining_state_.current_mineral_id;
    uint64_t current_hp = mining_state_.current_hp;
    uint64_t respawn_remaining_ms = 0;
    if (mining_state_.respawn_timer_ms > 0.0f)
    {
        respawn_remaining_ms = static_cast<uint64_t>(mining_state_.respawn_timer_ms);
    }

    if (mineral_id == 0)
    {
        auto game_data = game_repo_.get_user_game_data(user_id_);
        if (game_data.current_mineral_id.has_value())
        {
            mineral_id = game_data.current_mineral_id.value();
            current_hp = game_data.current_mineral_hp.value_or(0);
        }
    }

    const auto *mineral = metadata_.mineral(mineral_id);
    if (res.error_code().empty())
    {
        if (mineral_id == 0)
        {
            res.set_error_code("MINERAL_NOT_SELECTED");
        }
        else if (mineral == nullptr)
        {
            res.set_error_code("INVALID_MINERAL");
        }
    }

    auto slots_response = slot_service_.handle_all_slots(user_id_);
    uint64_t total_dps = slots_response.total_dps();
    if (res.error_code().empty() && total_dps == 0)
    {
        res.set_error_code("DPS_ZERO");
    }

    if (res.error_code().empty() && mineral != nullptr)
    {
        if (current_hp > mineral->hp)
        {
            current_hp = mineral->hp;
        }
    }

    if (res.error_code().empty())
    {
        const std::string key = "offline:mode:" + user_id_;
        std::unordered_map<std::string, std::string> fields{
            {"start_ms", std::to_string(now_ms_utc())},
            {"available_seconds", std::to_string(offline_state.current_offline_seconds)},
            {"mineral_id", std::to_string(mineral_id)},
            {"current_hp", std::to_string(current_hp)},
            {"respawn_remaining_ms", std::to_string(respawn_remaining_ms)},
            {"total_dps", std::to_string(total_dps)}
        };

        if (!redis_.hset_fields(key, fields, std::chrono::seconds(kOfflineSessionTtlSeconds)))
        {
            res.set_error_code("REDIS_ERROR");
        }
    }

    if (res.error_code().empty())
    {
        res.set_success(true);
    }

    infinitepickaxe::Envelope response_env;
    response_env.set_type(infinitepickaxe::OFFLINE_MODE_START_RESULT);
    *response_env.mutable_offline_mode_start_result() = res;
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
    router_.register_handler(infinitepickaxe::WEEKLY_MISSIONS_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_weekly_missions(e); });
    router_.register_handler(infinitepickaxe::WEEKLY_MISSION_PROGRESS_UPDATE, [this](const infinitepickaxe::Envelope &e)
                             { handle_weekly_mission_progress_update(e); });
    router_.register_handler(infinitepickaxe::WEEKLY_MISSION_CLAIM, [this](const infinitepickaxe::Envelope &e)
                             { handle_weekly_mission_claim(e); });
    router_.register_handler(infinitepickaxe::WEEKLY_MILESTONE_CLAIM, [this](const infinitepickaxe::Envelope &e)
                             { handle_weekly_milestone_claim(e); });
    router_.register_handler(infinitepickaxe::ACHIEVEMENTS_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_achievements(e); });
    router_.register_handler(infinitepickaxe::ACHIEVEMENT_PROGRESS_UPDATE, [this](const infinitepickaxe::Envelope &e)
                             { handle_achievement_progress_update(e); });
    router_.register_handler(infinitepickaxe::ACHIEVEMENT_CLAIM, [this](const infinitepickaxe::Envelope &e)
                             { handle_achievement_claim(e); });
    router_.register_handler(infinitepickaxe::INFINITE_MINE_STATE_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_infinite_mine_state(e); });
    router_.register_handler(infinitepickaxe::INFINITE_MINE_CHALLENGE_START_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_infinite_mine_challenge_start(e); });
    router_.register_handler(infinitepickaxe::INFINITE_MINE_AUTO_CLAIM_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_infinite_mine_auto_claim(e); });
    router_.register_handler(infinitepickaxe::INFINITE_MINE_AUTO_CLAIM_ALL_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_infinite_mine_auto_claim_all(e); });
    router_.register_handler(infinitepickaxe::INFINITE_MINE_EXIT_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_infinite_mine_exit(e); });
    router_.register_handler(infinitepickaxe::AD_WATCH_COMPLETE, [this](const infinitepickaxe::Envelope &e)
                             { handle_ad_watch(e); });
    router_.register_handler(infinitepickaxe::MILESTONE_CLAIM, [this](const infinitepickaxe::Envelope &e)
                             { handle_milestone_claim(e); });
    router_.register_handler(infinitepickaxe::MAIL_LIST_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_mail_list(e); });
    router_.register_handler(infinitepickaxe::MAIL_DETAIL_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_mail_detail(e); });
    router_.register_handler(infinitepickaxe::MAIL_CLAIM_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_mail_claim(e); });
    router_.register_handler(infinitepickaxe::MAIL_CLAIM_ALL_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_mail_claim_all(e); });
    router_.register_handler(infinitepickaxe::ITEM_INVENTORY_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_item_inventory(e); });
    router_.register_handler(infinitepickaxe::ITEM_INVENTORY_EXPAND_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_item_inventory_expand(e); });
    router_.register_handler(infinitepickaxe::USE_ITEM_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_use_item(e); });
    router_.register_handler(infinitepickaxe::SLOT_UNLOCK, [this](const infinitepickaxe::Envelope &e)
                             { handle_slot_unlock(e); });
    router_.register_handler(infinitepickaxe::ALL_SLOTS_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_all_slots(e); });
    router_.register_handler(infinitepickaxe::OFFLINE_REWARD_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_offline_reward(e); });
    router_.register_handler(infinitepickaxe::OFFLINE_MODE_START_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_offline_mode_start(e); });
    router_.register_handler(infinitepickaxe::GEM_LIST_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_gem_list(e); });
    router_.register_handler(infinitepickaxe::GEM_GACHA_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_gem_gacha(e); });
    router_.register_handler(infinitepickaxe::GEM_SYNTHESIS_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_gem_synthesis(e); });
    router_.register_handler(infinitepickaxe::GEM_AUTO_SYNTHESIS_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_gem_auto_synthesis(e); });
    router_.register_handler(infinitepickaxe::GEM_CONVERSION_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_gem_conversion(e); });
    router_.register_handler(infinitepickaxe::GEM_DISCARD_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_gem_discard(e); });
    router_.register_handler(infinitepickaxe::GEM_EQUIP_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_gem_equip(e); });
    router_.register_handler(infinitepickaxe::GEM_UNEQUIP_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_gem_unequip(e); });
    router_.register_handler(infinitepickaxe::GEM_SLOT_UNLOCK_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_gem_slot_unlock(e); });
    router_.register_handler(infinitepickaxe::GEM_INVENTORY_EXPAND_REQUEST, [this](const infinitepickaxe::Envelope &e)
                             { handle_gem_inventory_expand(e); });
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
                             boost::asio::bind_executor(
                                 strand_,
                                 [self](boost::system::error_code ec, std::size_t /*written*/)
                                 {
                                     if (ec)
                                     {
                                         self->close();
                                         return;
                                     }
                                 }));
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

void Session::send_weekly_mission_progress_updates(
    const std::vector<infinitepickaxe::WeeklyMissionProgressUpdate>& updates)
{
    for (const auto& update : updates)
    {
        infinitepickaxe::Envelope env;
        env.set_type(infinitepickaxe::WEEKLY_MISSION_PROGRESS_UPDATE);
        *env.mutable_weekly_mission_progress_update() = update;
        send_envelope(env);
    }
}

void Session::send_achievement_progress_updates(const std::vector<infinitepickaxe::AchievementProgressUpdate>& updates)
{
    for (const auto& update : updates)
    {
        infinitepickaxe::Envelope env;
        env.set_type(infinitepickaxe::ACHIEVEMENT_PROGRESS_UPDATE);
        *env.mutable_achievement_progress_update() = update;
        send_envelope(env);
    }
}

void Session::send_daily_missions_state()
{
    auto res = mission_service_.get_missions(user_id_);
    infinitepickaxe::Envelope env;
    env.set_type(infinitepickaxe::DAILY_MISSIONS_RESPONSE);
    *env.mutable_daily_missions_response() = res;
    send_envelope(env);
}

void Session::send_milestone_state()
{
    auto state = mission_service_.get_milestone_state(user_id_);
    infinitepickaxe::Envelope env;
    env.set_type(infinitepickaxe::MILESTONE_STATE);
    *env.mutable_milestone_state() = state;
    send_envelope(env);
}

void Session::send_weekly_missions_state()
{
    auto res = mission_service_.get_weekly_missions(user_id_);
    infinitepickaxe::Envelope env;
    env.set_type(infinitepickaxe::WEEKLY_MISSIONS_RESPONSE);
    *env.mutable_weekly_missions_response() = res;
    send_envelope(env);
}

void Session::send_weekly_milestone_state()
{
    auto state = mission_service_.get_weekly_milestone_state(user_id_);
    infinitepickaxe::Envelope env;
    env.set_type(infinitepickaxe::WEEKLY_MILESTONE_STATE);
    *env.mutable_weekly_milestone_state() = state;
    send_envelope(env);
}

void Session::send_achievements_state()
{
    auto state = achievement_service_.get_state(user_id_);
    infinitepickaxe::Envelope env;
    env.set_type(infinitepickaxe::ACHIEVEMENTS_RESPONSE);
    *env.mutable_achievements_response() = state;
    send_envelope(env);
}

void Session::send_ad_counters_state()
{
    auto state = ad_service_.get_ad_counters_state(user_id_);
    infinitepickaxe::Envelope env;
    env.set_type(infinitepickaxe::AD_COUNTERS_STATE);
    *env.mutable_ad_counters_state() = state;
    send_envelope(env);
}

void Session::send_infinite_mine_state()
{
    auto state = infinite_mine_service_.get_state(user_id_);
    infinitepickaxe::Envelope env;
    env.set_type(infinitepickaxe::INFINITE_MINE_STATE_RESPONSE);
    *env.mutable_infinite_mine_state_response() = state;
    send_envelope(env);
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

bool Session::try_consume_offline_session(infinitepickaxe::OfflineRewardResult& out_result)
{
    if (user_id_.empty())
    {
        return false;
    }

    std::unordered_map<std::string, std::string> fields;
    const std::string key = "offline:mode:" + user_id_;
    if (!redis_.hgetall(key, fields) || fields.empty())
    {
        return false;
    }

    OfflineSessionData data;
    if (!parse_offline_session(fields, data))
    {
        redis_.delete_key(key);
        return false;
    }

    uint64_t now_ms = now_ms_utc();
    if (now_ms <= data.start_ms)
    {
        redis_.delete_key(key);
        return false;
    }

    uint64_t elapsed_seconds = (now_ms - data.start_ms) / 1000;
    if (elapsed_seconds > data.available_seconds)
    {
        elapsed_seconds = data.available_seconds;
    }

    uint32_t start_date = kst_date_yyyymmdd(data.start_ms);
    uint32_t end_date = kst_date_yyyymmdd(now_ms);
    uint32_t new_seconds = data.available_seconds;
    if (start_date != end_date)
    {
        new_seconds = offline_service_.initial_offline_seconds();
    }
    else
    {
        new_seconds = data.available_seconds > elapsed_seconds
            ? static_cast<uint32_t>(data.available_seconds - elapsed_seconds)
            : 0;
    }

    if (!offline_service_.set_offline_seconds_today(user_id_, new_seconds).has_value())
    {
        spdlog::warn("offline_seconds update failed: user={}", user_id_);
    }

    if (elapsed_seconds == 0)
    {
        redis_.delete_key(key);
        return false;
    }

    const auto *mineral = metadata_.mineral(data.mineral_id);
    OfflineMiningResult mining_result{};
    uint64_t clamped_hp = data.current_hp;
    if (mineral != nullptr && mineral->hp > 0 && clamped_hp > mineral->hp)
    {
        clamped_hp = mineral->hp;
    }

    if (mineral != nullptr && mineral->hp > 0 && data.total_dps > 0)
    {
        mining_result = simulate_offline_mining(*mineral,
                                                data.total_dps,
                                                clamped_hp,
                                                data.respawn_remaining_ms,
                                                elapsed_seconds);
    }
    else
    {
        mining_result.remaining_hp = clamped_hp;
        mining_result.respawn_remaining_ms = data.respawn_remaining_ms;
    }

    if (mineral != nullptr)
    {
        uint64_t new_hp = mining_result.remaining_hp;
        if (mineral->respawn_time == 0 && new_hp == 0)
        {
            new_hp = mineral->hp;
        }
        if (new_hp > mineral->hp)
        {
            new_hp = mineral->hp;
        }

        if (!game_repo_.set_current_mineral(user_id_, data.mineral_id, new_hp))
        {
            spdlog::warn("offline reward failed to update mineral state: user={} mineral_id={}",
                         user_id_, data.mineral_id);
        }

        uint64_t respawn_until_ms = 0;
        if (mining_result.respawn_remaining_ms > 0)
        {
            respawn_until_ms = now_ms + mining_result.respawn_remaining_ms;
        }

        std::unordered_map<std::string, std::string> cache_fields{
            {"mineral_id", std::to_string(data.mineral_id)},
            {"current_hp", std::to_string(new_hp)},
            {"max_hp", std::to_string(mineral->hp)},
            {"respawn_until_ms", std::to_string(respawn_until_ms)},
            {"updated_at", std::to_string(now_ms / 1000)}
        };
        const std::string mining_key = "session:mining:" + user_id_;
        redis_.hset_fields(mining_key, cache_fields, std::chrono::seconds(kMiningCacheTtlSeconds));
    }

    if (mining_result.gold_earned > 0 || mining_result.mining_count > 0)
    {
        mining_service_.apply_offline_reward(user_id_, mining_result.gold_earned, mining_result.mining_count);
    }

    out_result.set_elapsed_seconds(elapsed_seconds);
    out_result.set_gold_earned(mining_result.gold_earned);
    out_result.set_mining_count(mining_result.mining_count);
    out_result.set_total_gold(0);

    redis_.delete_key(key);
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
    auto weekly_updates = mission_service_.handle_weekly_play_time_seconds(user_id_, flush_seconds);
    send_weekly_mission_progress_updates(weekly_updates);
    auto achievement_updates = achievement_service_.handle_play_time_seconds(user_id_, flush_seconds);
    send_achievement_progress_updates(achievement_updates);
}

void Session::close(bool allow_grace)
{
    if (closed_)
        return;
    flush_play_time_progress(true);
    if (allow_grace && authenticated_)
    {
        cache_mining_state();
    }
    closed_ = true;
    boost::system::error_code timer_ec;
    auth_timer_.cancel(timer_ec);
    if (registry_ && !user_id_.empty())
    {
        registry_->remove_if_match(user_id_, this);
        if (allow_grace && authenticated_)
        {
            registry_->mark_disconnected(user_id_, device_id_);
        }
    }
    boost::system::error_code ignored;
    socket_.shutdown(boost::asio::ip::tcp::socket::shutdown_both, ignored);
    socket_.close(ignored);
}

void Session::start_auth_timer()
{
    auto self = shared_from_this();
    auth_timer_.expires_after(std::chrono::seconds(5));
    auth_timer_.async_wait([self](const boost::system::error_code &ec)
                           {
                               if (ec)
                                   return; // cancelled
                               if (!self->authenticated_)
                               {
                                   self->send_error("1007", "AUTH_TIMEOUT");
                                   self->close();
                               } });
}

void Session::update_mining_tick(float delta_ms)
{
    auto self = shared_from_this();
    boost::asio::post(strand_, [self, delta_ms]()
                      {
                          self->update_mining_tick_internal(delta_ms);
                      });
}

void Session::update_mining_tick_internal(float delta_ms)
{
    // 인증되지 않았거나 세션이 닫혔으면 무시
    if (!authenticated_ || closed_)
    {
        return;
    }

    const uint64_t now_ms = static_cast<uint64_t>(
        std::chrono::duration_cast<std::chrono::milliseconds>(
            std::chrono::system_clock::now().time_since_epoch()).count());
    if (next_daily_reset_ms_ == 0)
    {
        next_daily_reset_ms_ = kst_next_midnight_ms();
    }
    else if (now_ms >= next_daily_reset_ms_)
    {
        send_daily_missions_state();
        send_milestone_state();
        send_ad_counters_state();
        send_infinite_mine_state();
        next_daily_reset_ms_ = kst_next_midnight_ms();
    }

    if (next_weekly_reset_ms_ == 0)
    {
        next_weekly_reset_ms_ = kst_next_week_reset_ms();
    }
    else if (now_ms >= next_weekly_reset_ms_)
    {
        send_weekly_missions_state();
        send_weekly_milestone_state();
        next_weekly_reset_ms_ = kst_next_week_reset_ms();
    }

    play_time_accum_ms_ += delta_ms;
    flush_play_time_progress(false);
    mining_cache_accum_ms_ += delta_ms;
    if (mining_cache_accum_ms_ >= static_cast<float>(kMiningCacheFlushSeconds) * 1000.0f)
    {
        cache_mining_state();
        mining_cache_accum_ms_ = 0.0f;
    }

    if (infinite_mine_state_.is_active)
    {
        update_infinite_mine_tick(delta_ms);
        return;
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

void Session::update_infinite_mine_tick(float delta_ms)
{
    if (!infinite_mine_state_.is_challenging)
    {
        return;
    }

    infinite_mine_state_.update_accum_ms += delta_ms;
    constexpr float kUpdateStepMs = 100.0f;

    while (infinite_mine_state_.update_accum_ms >= kUpdateStepMs)
    {
        infinite_mine_state_.update_accum_ms -= kUpdateStepMs;

        if (infinite_mine_state_.remaining_ms == 0)
        {
            end_infinite_mine_challenge(false, infinitepickaxe::TIMEOUT);
            return;
        }

        uint64_t step_ms = static_cast<uint64_t>(kUpdateStepMs);
        if (infinite_mine_state_.remaining_ms < step_ms)
        {
            step_ms = infinite_mine_state_.remaining_ms;
        }

        infinite_mine_state_.remaining_ms -= step_ms;

        std::vector<infinitepickaxe::PickaxeAttack> attacks;
        uint64_t total_damage = 0;

        for (auto &slot : infinite_mine_state_.slots)
        {
            slot.next_attack_timer_ms -= static_cast<float>(step_ms);

            while (slot.next_attack_timer_ms <= 0.0f)
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
            if (infinite_mine_state_.current_hp > total_damage)
            {
                infinite_mine_state_.current_hp -= total_damage;
            }
            else
            {
                infinite_mine_state_.current_hp = 0;
            }
        }

        if (!attacks.empty())
        {
            send_infinite_mine_update(attacks);
        }

        if (infinite_mine_state_.current_hp == 0)
        {
            end_infinite_mine_challenge(true, infinitepickaxe::CLEARED);
            return;
        }

        if (infinite_mine_state_.remaining_ms == 0)
        {
            end_infinite_mine_challenge(false, infinitepickaxe::TIMEOUT);
            return;
        }
    }
}

void Session::build_infinite_mine_slots()
{
    infinite_mine_state_.slots.clear();
    auto slots_response = slot_service_.handle_all_slots(user_id_);

    for (const auto &slot_info : slots_response.slots())
    {
        if (!slot_info.is_unlocked())
        {
            continue;
        }

        InfiniteMineSlotState slot{};
        slot.slot_index = slot_info.slot_index();
        slot.attack_power = slot_info.attack_power();
        slot.attack_speed = static_cast<float>(slot_info.attack_speed()) / 10000.0f;
        if (slot.attack_speed <= 0.0f)
        {
            slot.attack_speed = 0.01f;
        }
        slot.critical_hit_percent = slot_info.critical_hit_percent();
        slot.critical_damage = slot_info.critical_damage();

        float attack_interval_ms = 1000.0f / slot.attack_speed;
        slot.next_attack_timer_ms = (float)(std::rand() % 1000) / 1000.0f * attack_interval_ms;

        infinite_mine_state_.slots.push_back(slot);
    }
}

void Session::send_infinite_mine_update(const std::vector<infinitepickaxe::PickaxeAttack> &attacks)
{
    infinitepickaxe::InfiniteMineChallengeUpdate update;
    update.set_floor(infinite_mine_state_.floor);
    update.set_current_hp(infinite_mine_state_.current_hp);
    update.set_max_hp(infinite_mine_state_.max_hp);
    update.set_remaining_ms(infinite_mine_state_.remaining_ms);
    update.set_server_timestamp(static_cast<uint64_t>(
        std::chrono::duration_cast<std::chrono::milliseconds>(
            std::chrono::system_clock::now().time_since_epoch())
            .count()));

    for (const auto &attack : attacks)
    {
        *update.add_attacks() = attack;
    }

    infinitepickaxe::Envelope env;
    env.set_type(infinitepickaxe::INFINITE_MINE_CHALLENGE_UPDATE);
    *env.mutable_infinite_mine_challenge_update() = update;
    send_envelope(env);
}

void Session::end_infinite_mine_challenge(bool success, infinitepickaxe::InfiniteMineChallengeResultReason reason)
{
    if (!infinite_mine_state_.is_challenging)
    {
        return;
    }

    infinite_mine_state_.is_challenging = false;

    infinitepickaxe::InfiniteMineChallengeResult result;
    if (success && reason == infinitepickaxe::CLEARED)
    {
        result = infinite_mine_service_.handle_clear(user_id_, infinite_mine_state_.floor);
        if (!result.success())
        {
            result.set_reason(infinitepickaxe::INFINITE_MINE_RESULT_UNKNOWN);
        }
    }
    else
    {
        result.set_success(false);
        result.set_floor(infinite_mine_state_.floor);
        result.set_reason(reason);
        result.set_reward_gold(0);
        result.set_reward_crystal(0);

        auto data = game_repo_.get_user_game_data(user_id_);
        result.set_total_gold(data.gold);
        result.set_total_crystal(data.crystal);
    }

    infinitepickaxe::Envelope env;
    env.set_type(infinitepickaxe::INFINITE_MINE_CHALLENGE_RESULT);
    *env.mutable_infinite_mine_challenge_result() = result;
    send_envelope(env);

    if (success && result.success())
    {
        send_infinite_mine_state();
    }

    infinite_mine_state_.current_hp = 0;
    infinite_mine_state_.max_hp = 0;
    infinite_mine_state_.remaining_ms = 0;
    infinite_mine_state_.update_accum_ms = 0.0f;
    infinite_mine_state_.slots.clear();
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
        slot.attack_speed = static_cast<float>(slot_info.attack_speed()) / 10000.0f;
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
    auto weekly_updates = mission_service_.handle_weekly_mining_complete(user_id_, mining_state_.current_mineral_id);
    auto weekly_gold_updates = mission_service_.handle_weekly_gold_earned(user_id_, completion_result.gold_earned());
    weekly_updates.insert(weekly_updates.end(), weekly_gold_updates.begin(), weekly_gold_updates.end());
    send_weekly_mission_progress_updates(weekly_updates);
    auto achievement_updates = achievement_service_.handle_mining_complete(user_id_);
    auto achievement_gold_updates = achievement_service_.handle_gold_earned(user_id_, completion_result.gold_earned());
    achievement_updates.insert(achievement_updates.end(),
                               achievement_gold_updates.begin(),
                               achievement_gold_updates.end());
    send_achievement_progress_updates(achievement_updates);
    cache_mining_state();

    spdlog::info("Mining completed: user={} mineral={} gold_earned={} respawn_time={}s",
                 user_id_, mining_state_.current_mineral_id, gold_reward, respawn_time_sec);
}

// ========== 보석 핸들러 ==========

void Session::handle_gem_list(const infinitepickaxe::Envelope &env)
{
    if (!authenticated_) {
        send_error("NOT_AUTHENTICATED", "authentication required");
        return;
    }

    auto response = gem_service_.handle_gem_list(user_id_);

    infinitepickaxe::Envelope res_env;
    res_env.set_type(infinitepickaxe::GEM_LIST_RESPONSE);
    *res_env.mutable_gem_list_response() = response;
    send_envelope(res_env);
}

void Session::handle_gem_gacha(const infinitepickaxe::Envelope &env)
{
    if (!authenticated_) {
        send_error("NOT_AUTHENTICATED", "authentication required");
        return;
    }

    if (!env.has_gem_gacha_request()) {
        send_error("INVALID_REQUEST", "missing gem_gacha_request");
        return;
    }

    const auto& req = env.gem_gacha_request();
    auto result = gem_service_.handle_gacha_pull(user_id_, req.pull_count());

    infinitepickaxe::Envelope res_env;
    res_env.set_type(infinitepickaxe::GEM_GACHA_RESULT);
    *res_env.mutable_gem_gacha_result() = result;
    send_envelope(res_env);

    if (result.success()) {
        uint32_t created_count = static_cast<uint32_t>(result.gems_size());
        auto updates = mission_service_.handle_gem_created(user_id_, created_count);
        send_mission_progress_updates(updates);
        auto weekly_updates = mission_service_.handle_weekly_gem_created(user_id_, created_count);
        send_weekly_mission_progress_updates(weekly_updates);
        auto achievement_updates = achievement_service_.handle_gem_created(user_id_, created_count);
        send_achievement_progress_updates(achievement_updates);
    }
}

void Session::handle_gem_synthesis(const infinitepickaxe::Envelope &env)
{
    if (!authenticated_) {
        send_error("NOT_AUTHENTICATED", "authentication required");
        return;
    }

    if (!env.has_gem_synthesis_request()) {
        send_error("INVALID_REQUEST", "missing gem_synthesis_request");
        return;
    }

    const auto& req = env.gem_synthesis_request();
    std::vector<std::string> gem_ids;
    for (int i = 0; i < req.gem_instance_ids_size(); ++i) {
        gem_ids.push_back(req.gem_instance_ids(i));
    }

    auto result = gem_service_.handle_synthesis(user_id_, gem_ids);

    infinitepickaxe::Envelope res_env;
    res_env.set_type(infinitepickaxe::GEM_SYNTHESIS_RESULT);
    *res_env.mutable_gem_synthesis_result() = result;
    send_envelope(res_env);

    if (result.success()) {
        std::vector<infinitepickaxe::MissionProgressUpdate> updates;
        auto synthesis_updates = mission_service_.handle_gem_synthesis(user_id_, 1);
        updates.insert(updates.end(), synthesis_updates.begin(), synthesis_updates.end());

        if (result.synthesis_success()) {
            auto create_updates = mission_service_.handle_gem_created(user_id_, 1);
            updates.insert(updates.end(), create_updates.begin(), create_updates.end());
        }

        send_mission_progress_updates(updates);

        std::vector<infinitepickaxe::WeeklyMissionProgressUpdate> weekly_updates;
        auto weekly_synthesis_updates = mission_service_.handle_weekly_gem_synthesis(user_id_, 1);
        weekly_updates.insert(weekly_updates.end(),
                              weekly_synthesis_updates.begin(), weekly_synthesis_updates.end());

        if (result.synthesis_success()) {
            auto weekly_create_updates = mission_service_.handle_weekly_gem_created(user_id_, 1);
            weekly_updates.insert(weekly_updates.end(),
                                  weekly_create_updates.begin(), weekly_create_updates.end());
        }

        send_weekly_mission_progress_updates(weekly_updates);

        std::vector<infinitepickaxe::AchievementProgressUpdate> achievement_updates;
        uint32_t success_count = result.synthesis_success() ? 1 : 0;
        auto achievement_synthesis_updates = achievement_service_.handle_gem_synthesis(user_id_, 1, success_count);
        achievement_updates.insert(achievement_updates.end(),
                                   achievement_synthesis_updates.begin(),
                                   achievement_synthesis_updates.end());

        if (result.synthesis_success()) {
            auto achievement_create_updates = achievement_service_.handle_gem_created(user_id_, 1);
            achievement_updates.insert(achievement_updates.end(),
                                       achievement_create_updates.begin(),
                                       achievement_create_updates.end());
        }

        send_achievement_progress_updates(achievement_updates);
    }
}

void Session::handle_gem_auto_synthesis(const infinitepickaxe::Envelope &env)
{
    if (!authenticated_) {
        send_error("NOT_AUTHENTICATED", "authentication required");
        return;
    }

    if (!env.has_gem_auto_synthesis_request()) {
        send_error("INVALID_REQUEST", "missing gem_auto_synthesis_request");
        return;
    }

    const auto& req = env.gem_auto_synthesis_request();
    auto result = gem_service_.handle_auto_synthesis(user_id_, req.from_grade(), req.max_attempts());

    infinitepickaxe::Envelope res_env;
    res_env.set_type(infinitepickaxe::GEM_AUTO_SYNTHESIS_RESULT);
    *res_env.mutable_gem_auto_synthesis_result() = result;
    send_envelope(res_env);

    if (result.success()) {
        std::vector<infinitepickaxe::MissionProgressUpdate> updates;
        auto synthesis_updates = mission_service_.handle_gem_synthesis(user_id_, result.attempted());
        updates.insert(updates.end(), synthesis_updates.begin(), synthesis_updates.end());

        if (result.success_count() > 0) {
            auto create_updates = mission_service_.handle_gem_created(user_id_, result.success_count());
            updates.insert(updates.end(), create_updates.begin(), create_updates.end());
        }

        send_mission_progress_updates(updates);

        std::vector<infinitepickaxe::WeeklyMissionProgressUpdate> weekly_updates;
        auto weekly_synthesis_updates = mission_service_.handle_weekly_gem_synthesis(user_id_, result.attempted());
        weekly_updates.insert(weekly_updates.end(),
                              weekly_synthesis_updates.begin(), weekly_synthesis_updates.end());

        if (result.success_count() > 0) {
            auto weekly_create_updates = mission_service_.handle_weekly_gem_created(user_id_, result.success_count());
            weekly_updates.insert(weekly_updates.end(),
                                  weekly_create_updates.begin(), weekly_create_updates.end());
        }

        send_weekly_mission_progress_updates(weekly_updates);

        std::vector<infinitepickaxe::AchievementProgressUpdate> achievement_updates;
        auto achievement_synthesis_updates = achievement_service_.handle_gem_synthesis(
            user_id_, result.attempted(), result.success_count());
        achievement_updates.insert(achievement_updates.end(),
                                   achievement_synthesis_updates.begin(),
                                   achievement_synthesis_updates.end());

        if (result.success_count() > 0) {
            auto achievement_create_updates = achievement_service_.handle_gem_created(user_id_, result.success_count());
            achievement_updates.insert(achievement_updates.end(),
                                       achievement_create_updates.begin(),
                                       achievement_create_updates.end());
        }

        send_achievement_progress_updates(achievement_updates);
    }
}

void Session::handle_gem_conversion(const infinitepickaxe::Envelope &env)
{
    if (!authenticated_) {
        send_error("NOT_AUTHENTICATED", "authentication required");
        return;
    }

    if (!env.has_gem_conversion_request()) {
        send_error("INVALID_REQUEST", "missing gem_conversion_request");
        return;
    }

    const auto& req = env.gem_conversion_request();
    auto result = gem_service_.handle_conversion(user_id_,
                                                  req.gem_instance_id(),
                                                  req.target_type(),
                                                  req.use_fixed_cost());

    infinitepickaxe::Envelope res_env;
    res_env.set_type(infinitepickaxe::GEM_CONVERSION_RESULT);
    *res_env.mutable_gem_conversion_result() = result;
    send_envelope(res_env);

    if (result.success()) {
        auto updates = mission_service_.handle_gem_conversion(user_id_, 1);
        send_mission_progress_updates(updates);
        auto weekly_updates = mission_service_.handle_weekly_gem_conversion(user_id_, 1);
        send_weekly_mission_progress_updates(weekly_updates);
        auto achievement_updates = achievement_service_.handle_gem_conversion(user_id_, 1);
        send_achievement_progress_updates(achievement_updates);
    }
}

void Session::handle_gem_discard(const infinitepickaxe::Envelope &env)
{
    if (!authenticated_) {
        send_error("NOT_AUTHENTICATED", "authentication required");
        return;
    }

    if (!env.has_gem_discard_request()) {
        send_error("INVALID_REQUEST", "missing gem_discard_request");
        return;
    }

    const auto& req = env.gem_discard_request();
    std::vector<std::string> gem_ids;
    for (int i = 0; i < req.gem_instance_ids_size(); ++i) {
        gem_ids.push_back(req.gem_instance_ids(i));
    }

    auto result = gem_service_.handle_discard(user_id_, gem_ids);

    infinitepickaxe::Envelope res_env;
    res_env.set_type(infinitepickaxe::GEM_DISCARD_RESULT);
    *res_env.mutable_gem_discard_result() = result;
    send_envelope(res_env);

    if (result.success()) {
        uint32_t discard_count = static_cast<uint32_t>(gem_ids.size());
        auto updates = mission_service_.handle_gem_discard(user_id_, discard_count);
        send_mission_progress_updates(updates);
        auto weekly_updates = mission_service_.handle_weekly_gem_discard(user_id_, discard_count);
        send_weekly_mission_progress_updates(weekly_updates);
        auto achievement_updates = achievement_service_.handle_gem_discard(user_id_, discard_count);
        send_achievement_progress_updates(achievement_updates);
    }
}

void Session::handle_gem_equip(const infinitepickaxe::Envelope &env)
{
    if (!authenticated_) {
        send_error("NOT_AUTHENTICATED", "authentication required");
        return;
    }

    if (!env.has_gem_equip_request()) {
        send_error("INVALID_REQUEST", "missing gem_equip_request");
        return;
    }

    const auto& req = env.gem_equip_request();
    auto result = gem_service_.handle_equip(user_id_,
                                            req.pickaxe_slot_index(),
                                            req.gem_slot_index(),
                                            req.gem_instance_id());

    // 곡괭이 스탯이 변경되었으므로 채굴 시뮬레이션 슬롯 새로고침
    refresh_slots_from_service(true);

    infinitepickaxe::Envelope res_env;
    res_env.set_type(infinitepickaxe::GEM_EQUIP_RESULT);
    *res_env.mutable_gem_equip_result() = result;
    send_envelope(res_env);
}

void Session::handle_gem_unequip(const infinitepickaxe::Envelope &env)
{
    if (!authenticated_) {
        send_error("NOT_AUTHENTICATED", "authentication required");
        return;
    }

    if (!env.has_gem_unequip_request()) {
        send_error("INVALID_REQUEST", "missing gem_unequip_request");
        return;
    }

    const auto& req = env.gem_unequip_request();
    auto result = gem_service_.handle_unequip(user_id_,
                                              req.pickaxe_slot_index(),
                                              req.gem_slot_index());

    // 곡괭이 스탯이 변경되었으므로 채굴 시뮬레이션 슬롯 새로고침
    refresh_slots_from_service(true);

    infinitepickaxe::Envelope res_env;
    res_env.set_type(infinitepickaxe::GEM_UNEQUIP_RESULT);
    *res_env.mutable_gem_unequip_result() = result;
    send_envelope(res_env);
}

void Session::handle_gem_slot_unlock(const infinitepickaxe::Envelope &env)
{
    if (!authenticated_) {
        send_error("NOT_AUTHENTICATED", "authentication required");
        return;
    }

    if (!env.has_gem_slot_unlock_request()) {
        send_error("INVALID_REQUEST", "missing gem_slot_unlock_request");
        return;
    }

    const auto& req = env.gem_slot_unlock_request();
    auto result = gem_service_.handle_slot_unlock(user_id_,
                                                   req.pickaxe_slot_index(),
                                                   req.gem_slot_index());

    infinitepickaxe::Envelope res_env;
    res_env.set_type(infinitepickaxe::GEM_SLOT_UNLOCK_RESULT);
    *res_env.mutable_gem_slot_unlock_result() = result;
    send_envelope(res_env);
}

void Session::handle_gem_inventory_expand(const infinitepickaxe::Envelope &env)
{
    if (!authenticated_) {
        send_error("NOT_AUTHENTICATED", "authentication required");
        return;
    }

    if (!env.has_gem_inventory_expand_request()) {
        send_error("INVALID_REQUEST", "missing gem_inventory_expand_request");
        return;
    }

    auto result = gem_service_.handle_inventory_expand(user_id_);

    infinitepickaxe::Envelope res_env;
    res_env.set_type(infinitepickaxe::GEM_INVENTORY_EXPAND_RESULT);
    *res_env.mutable_gem_inventory_expand_result() = result;
    send_envelope(res_env);
}
