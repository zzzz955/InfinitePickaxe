#include "mission_service.h"
#include <spdlog/spdlog.h>
#include <chrono>
#include <random>

namespace {
uint32_t reward_for_ad_view(const std::vector<uint32_t>& rewards_by_view, uint32_t count) {
    if (count == 0) return 0;
    if (count > rewards_by_view.size()) return 0;
    return rewards_by_view[count - 1];
}
} // namespace

// Daily missions (3 slots)
infinitepickaxe::DailyMissionsResponse MissionService::get_missions(const std::string& user_id) {
    infinitepickaxe::DailyMissionsResponse response;

    auto daily_info = repo_.get_or_create_daily_mission_info(user_id);
    response.set_completed_count(daily_info.completed_count);
    response.set_reroll_count(daily_info.reroll_count);

    auto slots = repo_.get_all_mission_slots(user_id);
    if (slots.size() < 3) {
        bool changed = false;
        std::vector<bool> has_slot(4, false);
        for (const auto& s : slots) {
            if (s.slot_no < has_slot.size()) has_slot[s.slot_no] = true;
        }
        for (uint32_t slot_no = 1; slot_no <= 3; ++slot_no) {
            if (!has_slot[slot_no]) {
                assign_random_mission(user_id, slot_no);
                changed = true;
            }
        }
        if (changed) {
            slots = repo_.get_all_mission_slots(user_id);
        }
    }

    for (const auto& slot : slots) {
        auto* entry = response.add_missions();
        entry->set_slot_no(slot.slot_no);
        entry->set_mission_id(slot.mission_id);
        entry->set_mission_type(slot.mission_type);

        std::string description = "mission";
        for (const auto& m : meta_.missions()) {
            if (m.type == slot.mission_type && m.target == slot.target_value) {
                description = m.description;
                break;
            }
        }
        entry->set_description(description);

        entry->set_target_value(slot.target_value);
        entry->set_current_value(slot.current_value);
        entry->set_reward_crystal(slot.reward_crystal);
        entry->set_status(slot.status);

        auto assigned_ms = std::chrono::duration_cast<std::chrono::milliseconds>(
            slot.assigned_at.time_since_epoch()).count();
        entry->set_assigned_at(assigned_ms);

        if (slot.expires_at.has_value()) {
            auto expires_ms = std::chrono::duration_cast<std::chrono::milliseconds>(
                slot.expires_at->time_since_epoch()).count();
            entry->set_expires_at(expires_ms);
        }
    }

    // Ad counters
    auto ad_counters = repo_.get_all_ad_counters(user_id);
    for (const auto& counter : ad_counters) {
        auto* ad_counter = response.add_ad_counters();
        ad_counter->set_ad_type(counter.ad_type);
        ad_counter->set_ad_count(counter.ad_count);
        uint32_t limit = 0;
        if (const auto* ad_meta = meta_.ad_meta(counter.ad_type)) {
            limit = ad_meta->daily_limit;
        }
        ad_counter->set_daily_limit(limit);
    }

    return response;
}

// Progress update
bool MissionService::update_mission_progress(const std::string& user_id, uint32_t slot_no,
                                             uint32_t new_value) {
    auto slot_opt = repo_.get_mission_slot(user_id, slot_no);
    if (!slot_opt.has_value()) {
        spdlog::warn("update_mission_progress: slot not found user={} slot={}", user_id, slot_no);
        return false;
    }

    auto& slot = slot_opt.value();

    if (slot.status == "completed" || slot.status == "claimed") {
        spdlog::debug("update_mission_progress: mission already done user={} slot={}", user_id, slot_no);
        return false;
    }

    std::string new_status = slot.status;
    if (new_value >= slot.target_value) {
        new_status = "completed";
    }

    bool success = repo_.update_mission_progress(user_id, slot_no, new_value, new_status);

    if (success && new_status == "completed") {
        repo_.complete_mission(user_id, slot_no);
        repo_.increment_completed_count(user_id, 1);
    }

    return success;
}

// Complete mission and grant reward
infinitepickaxe::MissionCompleteResult MissionService::claim_mission_reward(
    const std::string& user_id, uint32_t slot_no) {

    infinitepickaxe::MissionCompleteResult result;
    result.set_success(false);
    result.set_slot_no(slot_no);

    auto slot_opt = repo_.get_mission_slot(user_id, slot_no);
    if (!slot_opt.has_value()) {
        result.set_error_code("MISSION_NOT_FOUND");
        return result;
    }

    auto& slot = slot_opt.value();

    if (slot.status != "completed") {
        result.set_error_code("MISSION_NOT_COMPLETED");
        return result;
    }

    if (slot.status == "claimed") {
        result.set_error_code("ALREADY_CLAIMED");
        return result;
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

    result.set_success(true);
    result.set_mission_id(slot.mission_id);
    result.set_reward_crystal(slot.reward_crystal);
    result.set_total_crystal(total_crystal);
    result.set_error_code("");

    spdlog::debug("claim_mission_reward: user={} slot={} reward={} total_crystal={}",
                  user_id, slot_no, slot.reward_crystal, total_crystal);

    return result;
}

// Reroll missions
infinitepickaxe::MissionRerollResult MissionService::reroll_missions(const std::string& user_id) {
    infinitepickaxe::MissionRerollResult result;
    result.set_success(false);

    auto daily_info = repo_.get_or_create_daily_mission_info(user_id);

    const auto reroll_meta = meta_.mission_reroll();
    const uint32_t total_limit = reroll_meta.free_rerolls_per_day + reroll_meta.ad_rerolls_per_day;
    const uint32_t next_reroll = daily_info.reroll_count + 1;
    if (next_reroll > total_limit) {
        result.set_error_code("REROLL_LIMIT_EXCEEDED");
        return result;
    }

    if (next_reroll > reroll_meta.free_rerolls_per_day) {
        const uint32_t required_ads = next_reroll - reroll_meta.free_rerolls_per_day;
        uint32_t ad_limit = reroll_meta.ad_rerolls_per_day;
        if (const auto* ad_meta = meta_.ad_meta("mission_reroll")) {
            ad_limit = ad_meta->daily_limit;
        }
        if (required_ads > ad_limit) {
            result.set_error_code("AD_LIMIT_EXCEEDED");
            return result;
        }
        auto ad_counter = repo_.get_or_create_ad_counter(user_id, "mission_reroll");
        if (ad_counter.ad_count < required_ads) {
            result.set_error_code("AD_REQUIRED");
            return result;
        }
    }

    for (uint32_t slot_no = 1; slot_no <= 3; ++slot_no) {
        repo_.delete_mission_slot(user_id, slot_no);
    }

    for (uint32_t slot_no = 1; slot_no <= 3; ++slot_no) {
        if (!assign_random_mission(user_id, slot_no)) {
            spdlog::error("reroll_missions: failed to assign slot={}", slot_no);
        }
    }

    repo_.increment_reroll_count(user_id);

    auto slots = repo_.get_all_mission_slots(user_id);
    for (const auto& slot : slots) {
        auto* entry = result.add_rerolled_missions();
        entry->set_slot_no(slot.slot_no);
        entry->set_mission_id(slot.mission_id);
        entry->set_mission_type(slot.mission_type);

        std::string description = "mission";
        for (const auto& m : meta_.missions()) {
            if (m.type == slot.mission_type && m.target == slot.target_value) {
                description = m.description;
                break;
            }
        }
        entry->set_description(description);

        entry->set_target_value(slot.target_value);
        entry->set_current_value(slot.current_value);
        entry->set_reward_crystal(slot.reward_crystal);
        entry->set_status(slot.status);

        auto assigned_ms = std::chrono::duration_cast<std::chrono::milliseconds>(
            slot.assigned_at.time_since_epoch()).count();
        entry->set_assigned_at(assigned_ms);
    }

    result.set_success(true);
    result.set_rerolls_used(daily_info.reroll_count + 1);

    spdlog::debug("reroll_missions: user={} rerolls={}", user_id, daily_info.reroll_count + 1);

    return result;
}

std::vector<AdCounter> MissionService::get_ad_counters(const std::string& user_id) {
    return repo_.get_all_ad_counters(user_id);
}

// Ad watch processing
infinitepickaxe::AdWatchResult MissionService::handle_ad_watch(const std::string& user_id, const std::string& ad_type) {
    infinitepickaxe::AdWatchResult result;
    result.set_success(false);
    result.set_ad_type(ad_type);
    result.set_error_code("");

    const auto* ad_meta = meta_.ad_meta(ad_type);
    if (!ad_meta) {
        result.set_error_code("INVALID_AD_TYPE");
    } else {
        auto counter = repo_.get_or_create_ad_counter(user_id, ad_type);
        if (ad_meta->daily_limit > 0 && counter.ad_count >= ad_meta->daily_limit) {
            result.set_error_code("DAILY_LIMIT_REACHED");
        } else {
            uint32_t next_count = counter.ad_count + 1;
            if (!repo_.increment_ad_counter(user_id, ad_type)) {
                result.set_error_code("DB_ERROR");
            } else {
                uint32_t crystal_reward = 0;
                if (ad_meta->effect == "crystal_reward") {
                    crystal_reward = reward_for_ad_view(ad_meta->rewards_by_view, next_count);
                    if (crystal_reward > 0) {
                        auto total_opt = game_repo_.add_crystal(user_id, crystal_reward);
                        if (!total_opt.has_value()) {
                            result.set_error_code("DB_ERROR");
                        } else {
                            result.set_total_crystal(total_opt.value());
                        }
                    }
                }
                result.set_crystal_earned(crystal_reward);
                if (result.error_code().empty()) {
                    result.set_success(true);
                }
            }
        }
    }

    auto ad_counters = repo_.get_all_ad_counters(user_id);
    for (const auto& counter_updated : ad_counters) {
        auto* ad_counter = result.add_ad_counters();
        ad_counter->set_ad_type(counter_updated.ad_type);
        ad_counter->set_ad_count(counter_updated.ad_count);
        uint32_t limit = 0;
        if (const auto* meta = meta_.ad_meta(counter_updated.ad_type)) {
            limit = meta->daily_limit;
        }
        ad_counter->set_daily_limit(limit);
    }

    if (!result.success() && result.error_code().empty()) {
        result.set_error_code("UNKNOWN_ERROR");
    }

    return result;
}

// Milestone claim (offline bonus hours)
infinitepickaxe::MilestoneClaimResult MissionService::handle_milestone_claim(
    const std::string& user_id, uint32_t milestone_count) {

    infinitepickaxe::MilestoneClaimResult res;
    res.set_success(false);
    res.set_milestone_count(milestone_count);

    uint32_t bonus_hours = 0;
    for (const auto& m : meta_.milestone_bonuses()) {
        if (m.completed == milestone_count) {
            bonus_hours = m.bonus_hours;
            break;
        }
    }

    if (bonus_hours == 0) {
        res.set_error_code("INVALID_MILESTONE");
        return res;
    }

    auto daily_info = repo_.get_or_create_daily_mission_info(user_id);
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

    res.set_success(true);
    res.set_offline_hours_gained(bonus_hours);
    res.set_total_offline_hours(updated_seconds.value() / 3600); // 프로토 필드가 hours 단위
    res.set_error_code("");
    return res;
}

// private: random mission assignment
bool MissionService::assign_random_mission(const std::string& user_id, uint32_t slot_no) {
    const auto& missions = meta_.missions();
    if (missions.empty()) {
        spdlog::error("assign_random_mission: no missions in metadata");
        return false;
    }

    static std::mt19937 rng(std::random_device{}());
    std::uniform_int_distribution<size_t> dist(0, missions.size() - 1);
    size_t idx = dist(rng);

    const auto& mission = missions[idx];

    bool success = repo_.assign_mission_to_slot(
        user_id, slot_no, mission.type, mission.target, mission.reward_crystal);

    if (success) {
        spdlog::debug("assign_random_mission: user={} slot={} type={} target={}",
                      user_id, slot_no, mission.type, mission.target);
    }

    return success;
}
