#include "mission_service.h"
#include "ad_service.h"
#include "time_utils.h"
#include <spdlog/spdlog.h>
#include <chrono>
#include <random>
#include <unordered_map>
#include <unordered_set>
#include <algorithm>
#include <cctype>
#include <ctime>
#include <sstream>
#include <limits>

namespace {
constexpr int kMissionCacheTtlSeconds = 60 * 60 * 48;
constexpr int kMissionFlushIntervalSeconds = 60 * 5;
constexpr int kWeeklyMissionCacheTtlSeconds = 60 * 60 * 24 * 8;

std::string kst_date_key() {
    auto now = std::chrono::system_clock::now() + std::chrono::hours(9);
    std::time_t tt = std::chrono::system_clock::to_time_t(now);
    std::tm tm = *std::gmtime(&tt);
    char buf[16];
    std::strftime(buf, sizeof(buf), "%Y%m%d", &tm);
    return std::string(buf);
}

std::string week_key_from_date(const std::string& date) {
    std::string key = date;
    key.erase(std::remove(key.begin(), key.end(), '-'), key.end());
    return key;
}

bool parse_uint32(const std::string& value, uint32_t& out) {
    if (value.empty()) return false;
    char* end = nullptr;
    unsigned long v = std::strtoul(value.c_str(), &end, 10);
    if (!end || *end != '\0') return false;
    if (v > std::numeric_limits<uint32_t>::max()) return false;
    out = static_cast<uint32_t>(v);
    return true;
}

uint32_t normalize_free_rerolls_used(uint32_t stored_rerolls, uint32_t free_limit) {
    if (free_limit == 0) {
        return 0;
    }
    if (stored_rerolls > free_limit) {
        uint32_t legacy_used = stored_rerolls - free_limit;
        return std::min(legacy_used, free_limit);
    }
    return stored_rerolls;
}

uint32_t resolve_max_daily_assign(const MetadataLoader& meta) {
    return meta.daily_missions_config().max_daily_assign;
}
} // namespace

// Daily missions (3 slots)
infinitepickaxe::DailyMissionsResponse MissionService::get_missions(const std::string& user_id) {
    infinitepickaxe::DailyMissionsResponse response;

    auto daily_info = ensure_daily_state_kst(user_id);
    response.set_completed_count(daily_info.completed_count);
    const uint32_t free_rerolls = meta_.mission_reroll().free_rerolls_per_day;
    const uint32_t free_used = normalize_free_rerolls_used(daily_info.reroll_count, free_rerolls);
    auto ad_counter = ad_service_.get_or_create_ad_counter(user_id, "mission_reroll");
    response.set_rerolls_free(free_rerolls);
    response.set_rerolls_total_limit(free_rerolls + meta_.mission_reroll().ad_rerolls_per_day);
    response.set_reset_timestamp_ms(kst_next_midnight_ms());
    response.set_reroll_count(free_used + ad_counter.ad_count);

    if (daily_info.reset_today) {
        for (uint32_t slot_no = 1; slot_no <= 3; ++slot_no) {
            repo_.delete_mission_slot(user_id, slot_no);
        }
        assign_random_missions_unique(user_id, 3);
    }

    auto slots = repo_.get_all_mission_slots(user_id);
    for (auto& slot : slots) {
        auto cached = load_cached_slot(user_id, slot.slot_no);
        if (cached.has_value() && cached->mission_id == slot.mission_id) {
            slot.current_value = cached->current_value;
            slot.status = cached->status;
        } else {
            cache_slot(user_id, slot);
        }
    }

    if (slots.size() < 3) {
        if (assign_random_missions_unique(user_id, 3 - static_cast<uint32_t>(slots.size()))) {
            slots = repo_.get_all_mission_slots(user_id);
        }
    }

    for (const auto& slot : slots) {
        const MissionMeta* meta = get_mission_meta_for_slot(slot);

        auto* entry = response.add_missions();
        entry->set_slot_no(slot.slot_no);
        entry->set_mission_id(slot.mission_id);
        entry->set_mission_type(slot.mission_type);
        entry->set_description(meta ? meta->description : "mission");
        entry->set_difficulty(meta ? meta->difficulty : "");
        entry->set_target_value(meta ? meta->target : slot.target_value);
        entry->set_current_value(slot.current_value);
        entry->set_reward_crystal(meta ? meta->reward_crystal : slot.reward_crystal);
        entry->set_status(slot.status);
        if (meta && meta->mineral_id.has_value()) {
            entry->mutable_mineral_id()->set_value(meta->mineral_id.value());
        }

        auto assigned_ms = std::chrono::duration_cast<std::chrono::milliseconds>(
            slot.assigned_at.time_since_epoch()).count();
        entry->set_assigned_at(assigned_ms);

        if (slot.expires_at.has_value()) {
            auto expires_ms = std::chrono::duration_cast<std::chrono::milliseconds>(
                slot.expires_at->time_since_epoch()).count();
            entry->set_expires_at(expires_ms);
        }
    }

    return response;
}

infinitepickaxe::WeeklyMissionsResponse MissionService::get_weekly_missions(const std::string& user_id) {
    infinitepickaxe::WeeklyMissionsResponse response;

    ensure_weekly_missions_assigned(user_id);

    const std::string week_start = current_week_start_date();
    const std::string week_key = week_key_from_date(week_start);

    auto missions = load_cached_weekly_missions(user_id, week_key);
    if (missions.size() < meta_.weekly_missions().size()) {
        std::vector<WeeklyMissionSeed> seeds;
        seeds.reserve(meta_.weekly_missions().size());
        for (const auto& meta : meta_.weekly_missions()) {
            if (meta.id == 0) {
                continue;
            }
            WeeklyMissionSeed seed;
            seed.mission_id = meta.id;
            seed.mission_type = meta.type;
            seed.target_value = meta.target;
            seed.reward_crystal = meta.reward_crystal;
            seeds.push_back(seed);
        }
        repo_.ensure_weekly_missions(user_id, week_start, seeds);
        missions = repo_.get_weekly_missions(user_id, week_start);
        cache_weekly_missions(user_id, week_key, missions);
    }

    response.set_claimed_count(repo_.get_weekly_claimed_count(user_id, week_start));
    response.set_reset_timestamp_ms(kst_next_week_reset_ms());

    for (const auto& mission : missions) {
        const MissionMeta* meta = get_weekly_mission_meta_by_id(mission.mission_id);
        auto* entry = response.add_missions();
        entry->set_mission_id(mission.mission_id);
        entry->set_mission_type(meta ? meta->type : mission.mission_type);
        entry->set_title(meta ? meta->title : "");
        entry->set_description(meta ? meta->description : "");
        entry->set_target_value(meta ? meta->target : mission.target_value);
        entry->set_current_value(mission.current_value);
        entry->set_reward_crystal(meta ? meta->reward_crystal : mission.reward_crystal);
        entry->set_reward_gold(0);
        entry->set_status(mission.status);
    }

    return response;
}

// Complete mission and grant reward
infinitepickaxe::MissionCompleteResult MissionService::claim_mission_reward(
    const std::string& user_id, uint32_t slot_no) {

    infinitepickaxe::MissionCompleteResult result;
    result.set_success(false);
    result.set_slot_no(slot_no);

    auto daily_info = ensure_daily_state_kst(user_id);
    const uint32_t max_daily_assign = resolve_max_daily_assign(meta_);
    if (max_daily_assign > 0 && daily_info.completed_count >= max_daily_assign) {
        result.set_error_code("DAILY_LIMIT_REACHED");
        return result;
    }

    auto cached = load_cached_slot(user_id, slot_no);
    const bool cache_found = cached.has_value();
    MissionSlot slot{};
    if (cached.has_value()) {
        slot = cached.value();
        if (slot.current_value >= slot.target_value && slot.status == "active") {
            slot.status = "completed";
        }
        if (slot.status == "claimed") {
            result.set_error_code("ALREADY_CLAIMED");
            return result;
        }
        if (slot.status != "completed") {
            result.set_error_code("MISSION_NOT_COMPLETED");
            return result;
        }
        flush_slot_to_db(user_id, slot);
    } else {
        auto slot_opt = repo_.get_mission_slot(user_id, slot_no);
        if (!slot_opt.has_value()) {
            result.set_error_code("MISSION_NOT_FOUND");
            return result;
        }
        slot = slot_opt.value();

        if (slot.status != "completed") {
            result.set_error_code("MISSION_NOT_COMPLETED");
            return result;
        }

        if (slot.status == "claimed") {
            result.set_error_code("ALREADY_CLAIMED");
            return result;
        }
    }

    if (!repo_.claim_mission_reward(user_id, slot_no)) {
        result.set_error_code("DB_ERROR");
        return result;
    }

    uint32_t total_crystal = 0;
    if (slot.reward_crystal > 0) {
        auto total_opt = game_repo_.add_crystal(user_id, slot.reward_crystal);
        if (!total_opt.has_value()) {
            result.set_error_code("DB_ERROR");
            return result;
        }
        total_crystal = total_opt.value();
    }

    repo_.increment_completed_count(user_id, 1);

    result.set_success(true);
    result.set_mission_id(slot.mission_id);
    result.set_reward_crystal(slot.reward_crystal);
    result.set_total_crystal(total_crystal);
    result.set_error_code("");

    slot.status = "claimed";
    if (cache_found) {
        update_slot_cache(user_id, slot);
    } else {
        cache_slot(user_id, slot);
    }

    spdlog::debug("claim_mission_reward: user={} slot={} reward={} total_crystal={}",
                  user_id, slot_no, slot.reward_crystal, total_crystal);

    return result;
}

infinitepickaxe::WeeklyMissionClaimResult MissionService::claim_weekly_mission_reward(
    const std::string& user_id, uint32_t mission_id) {

    infinitepickaxe::WeeklyMissionClaimResult result;
    result.set_success(false);
    result.set_mission_id(mission_id);

    ensure_weekly_missions_assigned(user_id);

    const std::string week_start = current_week_start_date();
    const std::string week_key = week_key_from_date(week_start);

    auto cached = load_cached_weekly_mission(user_id, week_key, mission_id);
    const bool cache_found = cached.has_value();
    WeeklyMission mission{};

    if (cache_found) {
        mission = cached.value();
        if (mission.current_value >= mission.target_value && mission.status == "active") {
            mission.status = "completed";
        }
        if (mission.status == "claimed") {
            result.set_error_code("ALREADY_CLAIMED");
            return result;
        }
        if (mission.status != "completed") {
            result.set_error_code("MISSION_NOT_COMPLETED");
            return result;
        }
        flush_weekly_mission_to_db(user_id, week_start, mission);
    } else {
        auto mission_opt = repo_.get_weekly_mission(user_id, week_start, mission_id);
        if (!mission_opt.has_value()) {
            result.set_error_code("MISSION_NOT_FOUND");
            return result;
        }
        mission = mission_opt.value();
        if (mission.status == "claimed") {
            result.set_error_code("ALREADY_CLAIMED");
            return result;
        }
        if (mission.status != "completed") {
            result.set_error_code("MISSION_NOT_COMPLETED");
            return result;
        }
    }

    if (!repo_.claim_weekly_mission_reward(user_id, week_start, mission_id)) {
        result.set_error_code("DB_ERROR");
        return result;
    }

    uint32_t total_crystal = 0;
    if (mission.reward_crystal > 0) {
        auto total_opt = game_repo_.add_crystal(user_id, mission.reward_crystal);
        if (!total_opt.has_value()) {
            result.set_error_code("DB_ERROR");
            return result;
        }
        total_crystal = total_opt.value();
    }

    uint64_t reward_gold = 0;
    uint64_t total_gold = 0;

    result.set_success(true);
    result.set_reward_crystal(mission.reward_crystal);
    result.set_reward_gold(reward_gold);
    result.set_total_crystal(total_crystal);
    result.set_total_gold(total_gold);
    result.set_error_code("");

    mission.status = "claimed";
    if (cache_found) {
        update_weekly_cache(user_id, week_key, mission);
    } else {
        cache_weekly_mission(user_id, week_key, mission);
    }

    spdlog::debug("claim_weekly_mission_reward: user={} mission_id={} reward_crystal={}",
                  user_id, mission_id, mission.reward_crystal);

    return result;
}

// Reroll missions (free request)
infinitepickaxe::MissionRerollResult MissionService::reroll_missions(const std::string& user_id) {
    return reroll_missions_internal(user_id, false);
}

// Reroll missions (ad request)
infinitepickaxe::MissionRerollResult MissionService::reroll_missions_ad(const std::string& user_id) {
    return reroll_missions_internal(user_id, true);
}

infinitepickaxe::MissionRerollResult MissionService::reroll_missions_internal(
    const std::string& user_id, bool use_ad) {
    infinitepickaxe::MissionRerollResult result;
    result.set_success(false);

    auto daily_info = ensure_daily_state_kst(user_id);
    const uint32_t max_daily_assign = resolve_max_daily_assign(meta_);
    if (max_daily_assign > 0 && daily_info.completed_count >= max_daily_assign) {
        result.set_error_code("DAILY_LIMIT_REACHED");
        return result;
    }

    const auto reroll_meta = meta_.mission_reroll();
    const uint32_t free_rerolls = reroll_meta.free_rerolls_per_day;
    const uint32_t free_used = normalize_free_rerolls_used(daily_info.reroll_count, free_rerolls);
    auto ad_counter = ad_service_.get_or_create_ad_counter(user_id, "mission_reroll");

    if (use_ad) {
        if (ad_counter.ad_count == 0) {
            result.set_error_code("AD_REQUIRED");
            return result;
        }
    } else {
        if (free_used >= free_rerolls) {
            result.set_error_code("AD_REQUIRED");
            return result;
        }
    }

    for (uint32_t slot_no = 1; slot_no <= 3; ++slot_no) {
        repo_.delete_mission_slot(user_id, slot_no);
    }

    assign_random_missions_unique(user_id, 3);

    if (!use_ad) {
        repo_.increment_reroll_count(user_id);
    }

    auto slots = repo_.get_all_mission_slots(user_id);
    cache_slots(user_id, slots);
    for (const auto& slot : slots) {
        const MissionMeta* meta = get_mission_meta_for_slot(slot);
        auto* entry = result.add_rerolled_missions();
        entry->set_slot_no(slot.slot_no);
        entry->set_mission_id(slot.mission_id);
        entry->set_mission_type(slot.mission_type);
        entry->set_description(meta ? meta->description : "mission");
        entry->set_difficulty(meta ? meta->difficulty : "");
        entry->set_target_value(meta ? meta->target : slot.target_value);
        entry->set_current_value(slot.current_value);
        entry->set_reward_crystal(meta ? meta->reward_crystal : slot.reward_crystal);
        entry->set_status(slot.status);
        if (meta && meta->mineral_id.has_value()) {
            entry->mutable_mineral_id()->set_value(meta->mineral_id.value());
        }

        auto assigned_ms = std::chrono::duration_cast<std::chrono::milliseconds>(
            slot.assigned_at.time_since_epoch()).count();
        entry->set_assigned_at(assigned_ms);
    }

    result.set_success(true);
    uint32_t rerolls_used = free_used + (use_ad ? 0 : 1) + ad_counter.ad_count;
    result.set_rerolls_used(rerolls_used);

    spdlog::debug("reroll_missions: user={} rerolls={} ad={}", user_id, rerolls_used, use_ad);

    return result;
}

// Milestone claim (offline bonus hours)
infinitepickaxe::MilestoneClaimResult MissionService::handle_milestone_claim(
    const std::string& user_id, uint32_t milestone_count) {

    infinitepickaxe::MilestoneClaimResult res;
    res.set_success(false);
    res.set_milestone_count(milestone_count);

    const MilestoneBonus* milestone_meta = nullptr;
    for (const auto& m : meta_.milestone_bonuses()) {
        if (m.completed == milestone_count) {
            milestone_meta = &m;
            break;
        }
    }

    if (!milestone_meta) {
        res.set_error_code("INVALID_MILESTONE");
        return res;
    }

    uint32_t bonus_hours = milestone_meta->bonus_hours;
    uint32_t milestone_crystal = milestone_meta->reward_crystal;
    if (bonus_hours == 0 && milestone_crystal == 0) {
        res.set_error_code("INVALID_MILESTONE");
        return res;
    }

    auto daily_info = ensure_daily_state_kst(user_id);
    if (daily_info.completed_count < milestone_count) {
        res.set_error_code("MILESTONE_NOT_REACHED");
        return res;
    }

    if (repo_.has_milestone_claimed(user_id, milestone_count)) {
        res.set_error_code("ALREADY_CLAIMED");
        return res;
    }

    if (!repo_.insert_milestone_claim(user_id, milestone_count)) {
        res.set_error_code("DB_ERROR");
        return res;
    }

    uint32_t bonus_seconds = bonus_hours * 3600;
    uint32_t initial_seconds = meta_.offline_defaults().initial_offline_seconds;
    auto updated_seconds = offline_repo_.add_offline_seconds(user_id, bonus_seconds, initial_seconds);
    if (!updated_seconds.has_value()) {
        res.set_error_code("DB_ERROR");
        return res;
    }

    // 크리스탈 보상: 메타데이터 reward_crystal 기준
    uint32_t total_crystal = 0;
    if (milestone_crystal > 0) {
        auto total_opt = game_repo_.add_crystal(user_id, milestone_crystal);
        if (!total_opt.has_value()) {
            res.set_error_code("DB_ERROR");
            return res;
        }
        total_crystal = total_opt.value();
    }

    res.set_success(true);
    res.set_offline_seconds_gained(bonus_seconds);
    res.set_total_offline_seconds(updated_seconds.value());
    res.set_reward_crystal(milestone_crystal);
    res.set_total_crystal(total_crystal);
    res.set_error_code("");
    return res;
}

infinitepickaxe::MilestoneState MissionService::get_milestone_state(const std::string& user_id) {
    infinitepickaxe::MilestoneState state;
    auto daily_info = ensure_daily_state_kst(user_id);
    state.set_completed_count(daily_info.completed_count);
    state.set_reset_timestamp_ms(kst_next_midnight_ms());

    auto claimed = repo_.get_claimed_milestones(user_id);
    for (auto milestone : claimed) {
        state.add_claimed_milestones(milestone);
    }
    return state;
}

infinitepickaxe::WeeklyMilestoneClaimResult MissionService::handle_weekly_milestone_claim(
    const std::string& user_id, uint32_t milestone_count) {

    infinitepickaxe::WeeklyMilestoneClaimResult res;
    res.set_success(false);
    res.set_milestone_count(milestone_count);

    const WeeklyMilestoneReward* milestone_meta = nullptr;
    for (const auto& m : meta_.weekly_milestone_rewards()) {
        if (m.completed == milestone_count) {
            milestone_meta = &m;
            break;
        }
    }

    if (!milestone_meta) {
        res.set_error_code("INVALID_MILESTONE");
        return res;
    }

    uint32_t reward_crystal = milestone_meta->reward_crystal;
    if (reward_crystal == 0) {
        res.set_error_code("INVALID_MILESTONE");
        return res;
    }

    const std::string week_start = current_week_start_date();
    uint32_t claimed_count = repo_.get_weekly_claimed_count(user_id, week_start);
    if (claimed_count < milestone_count) {
        res.set_error_code("MILESTONE_NOT_REACHED");
        return res;
    }

    if (repo_.has_weekly_milestone_claimed(user_id, week_start, milestone_count)) {
        res.set_error_code("ALREADY_CLAIMED");
        return res;
    }

    if (!repo_.insert_weekly_milestone_claim(user_id, week_start, milestone_count)) {
        res.set_error_code("DB_ERROR");
        return res;
    }

    uint32_t total_crystal = 0;
    if (reward_crystal > 0) {
        auto total_opt = game_repo_.add_crystal(user_id, reward_crystal);
        if (!total_opt.has_value()) {
            res.set_error_code("DB_ERROR");
            return res;
        }
        total_crystal = total_opt.value();
    }

    res.set_success(true);
    res.set_reward_crystal(reward_crystal);
    res.set_reward_gold(0);
    res.set_total_crystal(total_crystal);
    res.set_total_gold(0);
    res.set_error_code("");
    return res;
}

infinitepickaxe::WeeklyMilestoneState MissionService::get_weekly_milestone_state(
    const std::string& user_id) {
    infinitepickaxe::WeeklyMilestoneState state;
    const std::string week_start = current_week_start_date();
    state.set_claimed_count(repo_.get_weekly_claimed_count(user_id, week_start));
    state.set_reset_timestamp_ms(kst_next_week_reset_ms());

    auto claimed = repo_.get_weekly_claimed_milestones(user_id, week_start);
    for (auto milestone : claimed) {
        state.add_claimed_milestones(milestone);
    }
    return state;
}

std::vector<infinitepickaxe::MissionProgressUpdate> MissionService::handle_mining_complete(
    const std::string& user_id, uint32_t mineral_id) {
    return apply_progress_delta(user_id, [mineral_id](const MissionSlot& slot, const MissionMeta* meta) -> uint64_t {
        if (slot.mission_type == "mine_any") {
            return 1;
        }
        if (slot.mission_type == "mine_mineral" && meta && meta->mineral_id.has_value() &&
            meta->mineral_id.value() == mineral_id) {
            return 1;
        }
        return 0;
    });
}

std::vector<infinitepickaxe::MissionProgressUpdate> MissionService::handle_upgrade_try(
    const std::string& user_id, bool success) {
    return apply_progress_delta(user_id, [success](const MissionSlot& slot, const MissionMeta*) -> uint64_t {
        if (slot.mission_type == "upgrade_try") {
            return 1;
        }
        if (success && slot.mission_type == "upgrade_success") {
            return 1;
        }
        return 0;
    });
}

std::vector<infinitepickaxe::MissionProgressUpdate> MissionService::handle_gold_earned(
    const std::string& user_id, uint64_t gold_delta) {
    return apply_progress_delta(user_id, [gold_delta](const MissionSlot& slot, const MissionMeta*) -> uint64_t {
        if (slot.mission_type == "gold") {
            return gold_delta;
        }
        return 0;
    });
}

std::vector<infinitepickaxe::MissionProgressUpdate> MissionService::handle_play_time_seconds(
    const std::string& user_id, uint32_t seconds) {
    if (seconds == 0) {
        return {};
    }
    return apply_progress_delta(user_id, [seconds](const MissionSlot& slot, const MissionMeta*) -> uint64_t {
        if (slot.mission_type == "play_time") {
            return seconds;
        }
        return 0;
    });
}

std::vector<infinitepickaxe::MissionProgressUpdate> MissionService::handle_gem_created(
    const std::string& user_id, uint32_t created_count) {
    if (created_count == 0) {
        return {};
    }
    return apply_progress_delta(user_id, [created_count](const MissionSlot& slot, const MissionMeta*) -> uint64_t {
        if (slot.mission_type == "gem_create") {
            return created_count;
        }
        return 0;
    });
}

std::vector<infinitepickaxe::MissionProgressUpdate> MissionService::handle_gem_conversion(
    const std::string& user_id, uint32_t conversion_count) {
    if (conversion_count == 0) {
        return {};
    }
    return apply_progress_delta(user_id, [conversion_count](const MissionSlot& slot, const MissionMeta*) -> uint64_t {
        if (slot.mission_type == "gem_conversion") {
            return conversion_count;
        }
        return 0;
    });
}

std::vector<infinitepickaxe::MissionProgressUpdate> MissionService::handle_gem_synthesis(
    const std::string& user_id, uint32_t synthesis_count) {
    if (synthesis_count == 0) {
        return {};
    }
    return apply_progress_delta(user_id, [synthesis_count](const MissionSlot& slot, const MissionMeta*) -> uint64_t {
        if (slot.mission_type == "gem_synthesis") {
            return synthesis_count;
        }
        return 0;
    });
}

std::vector<infinitepickaxe::MissionProgressUpdate> MissionService::handle_gem_discard(
    const std::string& user_id, uint32_t discard_count) {
    if (discard_count == 0) {
        return {};
    }
    return apply_progress_delta(user_id, [discard_count](const MissionSlot& slot, const MissionMeta*) -> uint64_t {
        if (slot.mission_type == "gem_discard") {
            return discard_count;
        }
        return 0;
    });
}

std::vector<infinitepickaxe::WeeklyMissionProgressUpdate> MissionService::handle_weekly_mining_complete(
    const std::string& user_id, uint32_t mineral_id) {
    ensure_weekly_missions_assigned(user_id);
    return apply_weekly_progress_delta(user_id,
        [mineral_id](const WeeklyMission& mission, const MissionMeta* meta) -> uint64_t {
            if (mission.mission_type == "mine_any") {
                return 1;
            }
            if (mission.mission_type == "mine_mineral" && meta && meta->mineral_id.has_value() &&
                meta->mineral_id.value() == mineral_id) {
                return 1;
            }
            return 0;
        });
}

std::vector<infinitepickaxe::WeeklyMissionProgressUpdate> MissionService::handle_weekly_upgrade_try(
    const std::string& user_id, bool success) {
    ensure_weekly_missions_assigned(user_id);
    return apply_weekly_progress_delta(user_id,
        [success](const WeeklyMission& mission, const MissionMeta*) -> uint64_t {
            if (mission.mission_type == "upgrade_try") {
                return 1;
            }
            if (success && mission.mission_type == "upgrade_success") {
                return 1;
            }
            return 0;
        });
}

std::vector<infinitepickaxe::WeeklyMissionProgressUpdate> MissionService::handle_weekly_gold_earned(
    const std::string& user_id, uint64_t gold_delta) {
    if (gold_delta == 0) {
        return {};
    }
    ensure_weekly_missions_assigned(user_id);
    return apply_weekly_progress_delta(user_id,
        [gold_delta](const WeeklyMission& mission, const MissionMeta*) -> uint64_t {
            if (mission.mission_type == "gold") {
                return gold_delta;
            }
            return 0;
        });
}

std::vector<infinitepickaxe::WeeklyMissionProgressUpdate> MissionService::handle_weekly_play_time_seconds(
    const std::string& user_id, uint32_t seconds) {
    if (seconds == 0) {
        return {};
    }
    ensure_weekly_missions_assigned(user_id);
    return apply_weekly_progress_delta(user_id,
        [seconds](const WeeklyMission& mission, const MissionMeta*) -> uint64_t {
            if (mission.mission_type == "play_time") {
                return seconds;
            }
            return 0;
        });
}

std::vector<infinitepickaxe::WeeklyMissionProgressUpdate> MissionService::handle_weekly_gem_created(
    const std::string& user_id, uint32_t created_count) {
    if (created_count == 0) {
        return {};
    }
    ensure_weekly_missions_assigned(user_id);
    return apply_weekly_progress_delta(user_id,
        [created_count](const WeeklyMission& mission, const MissionMeta*) -> uint64_t {
            if (mission.mission_type == "gem_create") {
                return created_count;
            }
            return 0;
        });
}

std::vector<infinitepickaxe::WeeklyMissionProgressUpdate> MissionService::handle_weekly_gem_conversion(
    const std::string& user_id, uint32_t conversion_count) {
    if (conversion_count == 0) {
        return {};
    }
    ensure_weekly_missions_assigned(user_id);
    return apply_weekly_progress_delta(user_id,
        [conversion_count](const WeeklyMission& mission, const MissionMeta*) -> uint64_t {
            if (mission.mission_type == "gem_conversion") {
                return conversion_count;
            }
            return 0;
        });
}

std::vector<infinitepickaxe::WeeklyMissionProgressUpdate> MissionService::handle_weekly_gem_synthesis(
    const std::string& user_id, uint32_t synthesis_count) {
    if (synthesis_count == 0) {
        return {};
    }
    ensure_weekly_missions_assigned(user_id);
    return apply_weekly_progress_delta(user_id,
        [synthesis_count](const WeeklyMission& mission, const MissionMeta*) -> uint64_t {
            if (mission.mission_type == "gem_synthesis" || mission.mission_type == "gem_synthesis_try") {
                return synthesis_count;
            }
            return 0;
        });
}

std::vector<infinitepickaxe::WeeklyMissionProgressUpdate> MissionService::handle_weekly_gem_discard(
    const std::string& user_id, uint32_t discard_count) {
    if (discard_count == 0) {
        return {};
    }
    ensure_weekly_missions_assigned(user_id);
    return apply_weekly_progress_delta(user_id,
        [discard_count](const WeeklyMission& mission, const MissionMeta*) -> uint64_t {
            if (mission.mission_type == "gem_discard") {
                return discard_count;
            }
            return 0;
        });
}

// private helpers
DailyMissionInfo MissionService::ensure_daily_state_kst(const std::string& user_id) {
    return repo_.get_or_create_daily_mission_info(user_id);
}

bool MissionService::assign_random_missions_unique(const std::string& user_id, uint32_t count) {
    std::unordered_set<uint32_t> used_meta_ids;
    auto existing = repo_.get_all_mission_slots(user_id);
    for (const auto& slot : existing) {
        used_meta_ids.insert(slot.mission_id);
    }

    for (uint32_t slot_no = 1; slot_no <= 3 && count > 0; ++slot_no) {
        bool occupied = false;
        for (const auto& s : existing) {
            if (s.slot_no == slot_no) { occupied = true; break; }
        }
        if (occupied) continue;

        if (assign_random_mission(user_id, slot_no, used_meta_ids)) {
            --count;
        } else {
            spdlog::warn("assign_random_missions_unique: no mission assigned for slot {}", slot_no);
        }
    }
    return true;
}

bool MissionService::assign_random_mission(const std::string& user_id, uint32_t slot_no,
                                           std::unordered_set<uint32_t>& used_meta_ids) {
    const auto& missions = meta_.missions();
    if (missions.empty()) {
        spdlog::error("assign_random_mission: no missions in metadata");
        return false;
    }

    std::vector<const MissionMeta*> easy;
    std::vector<const MissionMeta*> medium;
    std::vector<const MissionMeta*> hard;

    for (const auto& m : missions) {
        if (used_meta_ids.count(m.id)) continue;
        std::string d = m.difficulty;
        std::transform(d.begin(), d.end(), d.begin(), ::tolower);
        if (d == "easy") easy.push_back(&m);
        else if (d == "hard") hard.push_back(&m);
        else medium.push_back(&m); // default medium
    }

    auto choose_pool = [&](std::mt19937& rng) -> std::vector<const MissionMeta*>* {
        struct Pool { std::vector<const MissionMeta*>* vec; uint32_t weight; };
        std::vector<Pool> pools;
        if (!easy.empty()) pools.push_back({&easy, 50});
        if (!medium.empty()) pools.push_back({&medium, 30});
        if (!hard.empty()) pools.push_back({&hard, 20});
        if (pools.empty()) return nullptr;

        uint32_t total_w = 0;
        for (auto& p : pools) total_w += p.weight;
        std::uniform_int_distribution<uint32_t> dist(1, total_w);
        uint32_t r = dist(rng);
        uint32_t acc = 0;
        for (auto& p : pools) {
            acc += p.weight;
            if (r <= acc) return p.vec;
        }
        return pools.back().vec;
    };

    static std::mt19937 rng(std::random_device{}());
    auto pool = choose_pool(rng);
    if (!pool || pool->empty()) {
        spdlog::warn("assign_random_mission: no pool available after filtering");
        return false;
    }

    std::uniform_int_distribution<size_t> dist(0, pool->size() - 1);
    const MissionMeta* mission = (*pool)[dist(rng)];
    if (!mission) return false;

    used_meta_ids.insert(mission->id);
    bool success = repo_.assign_mission_to_slot(
        user_id, slot_no, mission->id, mission->type, mission->target, mission->reward_crystal);

    if (success) {
        spdlog::debug("assign_random_mission: user={} slot={} mission_id={} type={} target={}",
                      user_id, slot_no, mission->id, mission->type, mission->target);
    }

    return success;
}

const MissionMeta* MissionService::get_mission_meta_by_id(uint32_t meta_id) const {
    for (const auto& m : meta_.missions()) {
        if (m.id == meta_id) return &m;
    }
    return nullptr;
}

const MissionMeta* MissionService::get_mission_meta_for_slot(const MissionSlot& slot) const {
    return get_mission_meta_by_id(slot.mission_id);
}

std::vector<infinitepickaxe::MissionProgressUpdate> MissionService::apply_progress_delta(
    const std::string& user_id,
    const std::function<uint64_t(const MissionSlot&, const MissionMeta*)>& delta_fn) {
    std::vector<infinitepickaxe::MissionProgressUpdate> updates;
    auto slots = load_cached_slots(user_id);
    bool any_updated = false;
    for (auto& slot : slots) {
        if (slot.status != "active") {
            continue;
        }
        const MissionMeta* meta = get_mission_meta_for_slot(slot);
        uint64_t delta = delta_fn(slot, meta);
        if (delta == 0) {
            continue;
        }

        uint64_t sum = static_cast<uint64_t>(slot.current_value) + delta;
        uint32_t target = slot.target_value;
        uint32_t new_value = static_cast<uint32_t>(sum > target ? target : sum);
        if (new_value == slot.current_value) {
            continue;
        }

        auto update_opt = apply_progress_update(user_id, slot, new_value);
        if (update_opt.has_value()) {
            updates.push_back(update_opt.value());
            any_updated = true;
            slot.current_value = new_value;
            if (new_value >= slot.target_value) {
                slot.status = "completed";
            }
        }
    }
    if (any_updated) {
        flush_slots_if_due(user_id, slots);
    }
    return updates;
}

std::optional<infinitepickaxe::MissionProgressUpdate> MissionService::apply_progress_update(
    const std::string& user_id, const MissionSlot& slot, uint32_t new_value) {
    std::string new_status = slot.status;
    if (new_value >= slot.target_value) {
        new_status = "completed";
    }

    MissionSlot updated = slot;
    updated.current_value = new_value;
    updated.status = new_status;
    update_slot_cache(user_id, updated);
    if (new_status == "completed") {
        flush_slot_to_db(user_id, updated);
    }

    infinitepickaxe::MissionProgressUpdate update;
    update.set_slot_no(slot.slot_no);
    update.set_mission_id(slot.mission_id);
    update.set_current_value(new_value);
    update.set_target_value(slot.target_value);
    update.set_status(new_status);
    return update;
}

std::vector<MissionSlot> MissionService::load_cached_slots(const std::string& user_id) {
    std::vector<MissionSlot> slots;
    bool all_cached = true;
    for (uint32_t slot_no = 1; slot_no <= 3; ++slot_no) {
        auto cached = load_cached_slot(user_id, slot_no);
        if (!cached.has_value()) {
            all_cached = false;
            break;
        }
        slots.push_back(cached.value());
    }

    if (all_cached && slots.size() == 3) {
        return slots;
    }

    slots = repo_.get_all_mission_slots(user_id);
    cache_slots(user_id, slots);
    return slots;
}

std::optional<MissionSlot> MissionService::load_cached_slot(const std::string& user_id, uint32_t slot_no) {
    const std::string key = "mission:slot:" + user_id + ":" + kst_date_key() + ":" + std::to_string(slot_no);
    std::unordered_map<std::string, std::string> fields;
    if (!redis_.hgetall(key, fields) || fields.empty()) {
        return std::nullopt;
    }

    MissionSlot slot;
    slot.user_id = user_id;
    slot.slot_no = slot_no;

    if (!parse_uint32(fields["mission_id"], slot.mission_id)) return std::nullopt;
    slot.mission_type = fields["mission_type"];
    if (!parse_uint32(fields["target_value"], slot.target_value)) return std::nullopt;
    if (!parse_uint32(fields["current_value"], slot.current_value)) return std::nullopt;
    if (!parse_uint32(fields["reward_crystal"], slot.reward_crystal)) return std::nullopt;
    slot.status = fields["status"];

    return slot;
}

void MissionService::cache_slot(const std::string& user_id, const MissionSlot& slot) {
    const std::string key = "mission:slot:" + user_id + ":" + kst_date_key() + ":" + std::to_string(slot.slot_no);
    std::unordered_map<std::string, std::string> fields{
        {"mission_id", std::to_string(slot.mission_id)},
        {"mission_type", slot.mission_type},
        {"target_value", std::to_string(slot.target_value)},
        {"current_value", std::to_string(slot.current_value)},
        {"reward_crystal", std::to_string(slot.reward_crystal)},
        {"status", slot.status},
    };
    redis_.hset_fields(key, fields, std::chrono::seconds(kMissionCacheTtlSeconds));
}

void MissionService::cache_slots(const std::string& user_id, const std::vector<MissionSlot>& slots) {
    for (const auto& slot : slots) {
        cache_slot(user_id, slot);
    }
}

void MissionService::update_slot_cache(const std::string& user_id, const MissionSlot& slot) {
    const std::string key = "mission:slot:" + user_id + ":" + kst_date_key() + ":" + std::to_string(slot.slot_no);
    std::unordered_map<std::string, std::string> fields{
        {"current_value", std::to_string(slot.current_value)},
        {"status", slot.status},
    };
    redis_.hset_fields(key, fields, std::chrono::seconds(kMissionCacheTtlSeconds));
}

bool MissionService::flush_slots_if_due(const std::string& user_id, const std::vector<MissionSlot>& slots) {
    const std::string key = "mission:flush:" + user_id + ":" + kst_date_key();
    auto now = std::chrono::system_clock::now();
    auto now_seconds = std::chrono::duration_cast<std::chrono::seconds>(now.time_since_epoch()).count();

    auto last_str = redis_.get_string(key);
    long long last = 0;
    if (last_str.has_value()) {
        try {
            last = std::stoll(last_str.value());
        } catch (...) {
            last = 0;
        }
    }

    if (last != 0 && (now_seconds - last) < kMissionFlushIntervalSeconds) {
        return false;
    }

    flush_slots_to_db(user_id, slots);
    redis_.set_string(key, std::to_string(now_seconds), std::chrono::seconds(kMissionCacheTtlSeconds));
    return true;
}

void MissionService::flush_slots_to_db(const std::string& user_id, const std::vector<MissionSlot>& slots) {
    for (const auto& slot : slots) {
        if (slot.status == "claimed") {
            continue;
        }
        flush_slot_to_db(user_id, slot);
    }
}

void MissionService::flush_slot_to_db(const std::string& user_id, const MissionSlot& slot) {
    repo_.update_mission_progress(user_id, slot.slot_no, slot.current_value, slot.status);
    if (slot.status == "completed") {
        repo_.complete_mission(user_id, slot.slot_no);
    }
}

std::string MissionService::current_week_start_date() const {
    return kst_week_start_date_string();
}

void MissionService::ensure_weekly_missions_assigned(const std::string& user_id) {
    const auto& missions = meta_.weekly_missions();
    if (missions.empty()) {
        return;
    }

    const std::string week_start = current_week_start_date();
    const std::string week_key = week_key_from_date(week_start);
    const std::string assigned_key = "weekly:assigned:" + user_id + ":" + week_key;
    if (redis_.get_string(assigned_key).has_value()) {
        return;
    }

    std::vector<WeeklyMissionSeed> seeds;
    seeds.reserve(missions.size());
    for (const auto& mission : missions) {
        if (mission.id == 0) {
            continue;
        }
        WeeklyMissionSeed seed;
        seed.mission_id = mission.id;
        seed.mission_type = mission.type;
        seed.target_value = mission.target;
        seed.reward_crystal = mission.reward_crystal;
        seeds.push_back(seed);
    }

    if (repo_.ensure_weekly_missions(user_id, week_start, seeds)) {
        redis_.set_string(assigned_key, "1", std::chrono::seconds(kWeeklyMissionCacheTtlSeconds));
    }
}

const MissionMeta* MissionService::get_weekly_mission_meta_by_id(uint32_t meta_id) const {
    for (const auto& m : meta_.weekly_missions()) {
        if (m.id == meta_id) return &m;
    }
    return nullptr;
}

std::vector<infinitepickaxe::WeeklyMissionProgressUpdate> MissionService::apply_weekly_progress_delta(
    const std::string& user_id,
    const std::function<uint64_t(const WeeklyMission&, const MissionMeta*)>& delta_fn) {
    std::vector<infinitepickaxe::WeeklyMissionProgressUpdate> updates;
    const std::string week_start = current_week_start_date();
    const std::string week_key = week_key_from_date(week_start);
    auto missions = load_cached_weekly_missions(user_id, week_key);
    bool any_updated = false;
    for (auto& mission : missions) {
        if (mission.status != "active") {
            continue;
        }
        const MissionMeta* meta = get_weekly_mission_meta_by_id(mission.mission_id);
        uint64_t delta = delta_fn(mission, meta);
        if (delta == 0) {
            continue;
        }

        uint64_t sum = static_cast<uint64_t>(mission.current_value) + delta;
        uint32_t target = mission.target_value;
        uint32_t new_value = static_cast<uint32_t>(sum > target ? target : sum);
        if (new_value == mission.current_value) {
            continue;
        }

        auto update_opt = apply_weekly_progress_update(user_id, mission, new_value);
        if (update_opt.has_value()) {
            updates.push_back(update_opt.value());
            any_updated = true;
            mission.current_value = new_value;
            if (new_value >= mission.target_value) {
                mission.status = "completed";
            }
        }
    }
    if (any_updated) {
        flush_weekly_missions_if_due(user_id, week_key, missions);
    }
    return updates;
}

std::optional<infinitepickaxe::WeeklyMissionProgressUpdate> MissionService::apply_weekly_progress_update(
    const std::string& user_id, const WeeklyMission& mission, uint32_t new_value) {
    const std::string week_start = current_week_start_date();
    const std::string week_key = week_key_from_date(week_start);

    std::string new_status = mission.status;
    if (new_value >= mission.target_value) {
        new_status = "completed";
    }

    WeeklyMission updated = mission;
    updated.current_value = new_value;
    updated.status = new_status;
    update_weekly_cache(user_id, week_key, updated);
    if (new_status == "completed") {
        flush_weekly_mission_to_db(user_id, week_start, updated);
    }

    infinitepickaxe::WeeklyMissionProgressUpdate update;
    update.set_mission_id(mission.mission_id);
    update.set_current_value(new_value);
    update.set_target_value(mission.target_value);
    update.set_status(new_status);
    return update;
}

std::vector<WeeklyMission> MissionService::load_cached_weekly_missions(
    const std::string& user_id, const std::string& week_key) {
    std::vector<WeeklyMission> missions;
    const auto& weekly_meta = meta_.weekly_missions();
    if (weekly_meta.empty()) {
        return missions;
    }

    bool all_cached = true;
    for (const auto& meta : weekly_meta) {
        if (meta.id == 0) {
            continue;
        }
        auto cached = load_cached_weekly_mission(user_id, week_key, meta.id);
        if (!cached.has_value()) {
            all_cached = false;
            break;
        }
        missions.push_back(cached.value());
    }

    if (all_cached && !missions.empty()) {
        return missions;
    }

    const std::string week_start = current_week_start_date();
    missions = repo_.get_weekly_missions(user_id, week_start);
    cache_weekly_missions(user_id, week_key, missions);
    return missions;
}

std::optional<WeeklyMission> MissionService::load_cached_weekly_mission(
    const std::string& user_id, const std::string& week_key, uint32_t mission_id) {
    const std::string key = "weekly:mission:" + user_id + ":" + week_key + ":" + std::to_string(mission_id);
    std::unordered_map<std::string, std::string> fields;
    if (!redis_.hgetall(key, fields) || fields.empty()) {
        return std::nullopt;
    }

    WeeklyMission mission;
    mission.user_id = user_id;
    mission.week_start_date = current_week_start_date();
    mission.mission_id = mission_id;
    mission.mission_type = fields["mission_type"];
    if (!parse_uint32(fields["target_value"], mission.target_value)) return std::nullopt;
    if (!parse_uint32(fields["current_value"], mission.current_value)) return std::nullopt;
    if (!parse_uint32(fields["reward_crystal"], mission.reward_crystal)) return std::nullopt;
    mission.status = fields["status"];

    return mission;
}

void MissionService::cache_weekly_mission(const std::string& user_id, const std::string& week_key,
                                          const WeeklyMission& mission) {
    const std::string key = "weekly:mission:" + user_id + ":" + week_key + ":" + std::to_string(mission.mission_id);
    std::unordered_map<std::string, std::string> fields{
        {"mission_type", mission.mission_type},
        {"target_value", std::to_string(mission.target_value)},
        {"current_value", std::to_string(mission.current_value)},
        {"reward_crystal", std::to_string(mission.reward_crystal)},
        {"status", mission.status},
    };
    redis_.hset_fields(key, fields, std::chrono::seconds(kWeeklyMissionCacheTtlSeconds));
}

void MissionService::cache_weekly_missions(const std::string& user_id, const std::string& week_key,
                                           const std::vector<WeeklyMission>& missions) {
    for (const auto& mission : missions) {
        cache_weekly_mission(user_id, week_key, mission);
    }
}

void MissionService::update_weekly_cache(const std::string& user_id, const std::string& week_key,
                                         const WeeklyMission& mission) {
    const std::string key = "weekly:mission:" + user_id + ":" + week_key + ":" + std::to_string(mission.mission_id);
    std::unordered_map<std::string, std::string> fields{
        {"current_value", std::to_string(mission.current_value)},
        {"status", mission.status},
    };
    redis_.hset_fields(key, fields, std::chrono::seconds(kWeeklyMissionCacheTtlSeconds));
}

bool MissionService::flush_weekly_missions_if_due(const std::string& user_id, const std::string& week_key,
                                                  const std::vector<WeeklyMission>& missions) {
    const std::string key = "weekly:flush:" + user_id + ":" + week_key;
    auto now = std::chrono::system_clock::now();
    auto now_seconds = std::chrono::duration_cast<std::chrono::seconds>(now.time_since_epoch()).count();

    auto last_str = redis_.get_string(key);
    long long last = 0;
    if (last_str.has_value()) {
        try {
            last = std::stoll(last_str.value());
        } catch (...) {
            last = 0;
        }
    }

    if (last != 0 && (now_seconds - last) < kMissionFlushIntervalSeconds) {
        return false;
    }

    const std::string week_start = current_week_start_date();
    flush_weekly_missions_to_db(user_id, week_start, missions);
    redis_.set_string(key, std::to_string(now_seconds), std::chrono::seconds(kWeeklyMissionCacheTtlSeconds));
    return true;
}

void MissionService::flush_weekly_missions_to_db(const std::string& user_id,
                                                 const std::string& week_start_date,
                                                 const std::vector<WeeklyMission>& missions) {
    for (const auto& mission : missions) {
        if (mission.status == "claimed") {
            continue;
        }
        flush_weekly_mission_to_db(user_id, week_start_date, mission);
    }
}

void MissionService::flush_weekly_mission_to_db(const std::string& user_id,
                                                const std::string& week_start_date,
                                                const WeeklyMission& mission) {
    repo_.update_weekly_mission_progress(user_id, week_start_date,
                                         mission.mission_id, mission.current_value, mission.status);
}
