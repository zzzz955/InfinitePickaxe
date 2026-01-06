#include "achievement_service.h"
#include <spdlog/spdlog.h>
#include <unordered_map>
#include <unordered_set>

infinitepickaxe::AchievementsResponse AchievementService::get_state(const std::string& user_id) {
    infinitepickaxe::AchievementsResponse response;

    auto counters = repo_.get_all_counters(user_id);
    std::unordered_map<std::string, uint64_t> counter_map;
    counter_map.reserve(counters.size());
    for (const auto& counter : counters) {
        counter_map[counter.achievement_type] = counter.current_value;
    }

    auto chains = repo_.get_all_chains(user_id);
    std::unordered_map<uint32_t, uint32_t> chain_map;
    chain_map.reserve(chains.size());
    for (const auto& chain : chains) {
        chain_map[chain.chain_id] = chain.last_claimed_step;
    }

    std::unordered_set<std::string> added_types;
    std::unordered_set<uint32_t> added_chains;

    for (const auto& meta : meta_.achievements()) {
        if (!meta.type.empty() && added_types.insert(meta.type).second) {
            auto* entry = response.add_progresses();
            entry->set_achievement_type(meta.type);
            auto it = counter_map.find(meta.type);
            entry->set_current_value(it != counter_map.end() ? it->second : 0);
        }

        if (meta.chain_id > 0 && added_chains.insert(meta.chain_id).second) {
            auto* chain = response.add_chains();
            chain->set_chain_id(meta.chain_id);
            auto it = chain_map.find(meta.chain_id);
            chain->set_last_claimed_step(it != chain_map.end() ? it->second : 0);
        }
    }

    for (const auto& pair : counter_map) {
        if (added_types.insert(pair.first).second) {
            auto* entry = response.add_progresses();
            entry->set_achievement_type(pair.first);
            entry->set_current_value(pair.second);
        }
    }

    for (const auto& pair : chain_map) {
        if (added_chains.insert(pair.first).second) {
            auto* chain = response.add_chains();
            chain->set_chain_id(pair.first);
            chain->set_last_claimed_step(pair.second);
        }
    }

    return response;
}

infinitepickaxe::AchievementClaimResult AchievementService::claim_achievement(const std::string& user_id,
                                                                             uint32_t achievement_id) {
    infinitepickaxe::AchievementClaimResult result;
    result.set_success(false);
    result.set_achievement_id(achievement_id);

    const auto* meta = meta_.achievement(achievement_id);
    if (!meta) {
        result.set_error_code("ACHIEVEMENT_NOT_FOUND");
        return result;
    }
    if (meta->type.empty() || meta->chain_id == 0 || meta->step_index == 0) {
        result.set_error_code("INVALID_META");
        return result;
    }

    const uint64_t progress = repo_.get_progress(user_id, meta->type).value_or(0);
    if (progress < meta->target) {
        result.set_error_code("NOT_COMPLETED");
        return result;
    }

    const uint32_t last_claimed = repo_.get_last_claimed_step(user_id, meta->chain_id).value_or(0);
    if (meta->step_index <= last_claimed) {
        result.set_error_code("ALREADY_CLAIMED");
        return result;
    }
    if (meta->step_index != last_claimed + 1) {
        result.set_error_code("PREVIOUS_STEP_NOT_CLAIMED");
        return result;
    }

    if (!repo_.try_claim_chain_step(user_id, meta->chain_id, last_claimed, meta->step_index)) {
        auto current_last = repo_.get_last_claimed_step(user_id, meta->chain_id).value_or(0);
        if (current_last >= meta->step_index) {
            result.set_error_code("ALREADY_CLAIMED");
            return result;
        }
        if (current_last + 1 != meta->step_index) {
            result.set_error_code("PREVIOUS_STEP_NOT_CLAIMED");
            return result;
        }
        spdlog::warn("achievement claim update failed: user={} chain_id={} expected_prev={} current_prev={} new_step={}",
                     user_id, meta->chain_id, last_claimed, current_last, meta->step_index);
        result.set_error_code("DB_ERROR");
        return result;
    }

    std::optional<uint32_t> total_crystal;
    std::optional<uint64_t> total_gold;
    if (meta->reward_crystal > 0) {
        auto total_opt = game_repo_.add_crystal(user_id, meta->reward_crystal);
        if (!total_opt.has_value()) {
            result.set_error_code("DB_ERROR");
            return result;
        }
        total_crystal = total_opt.value();
    }
    if (meta->reward_gold > 0) {
        auto total_opt = game_repo_.add_gold(user_id, meta->reward_gold);
        if (!total_opt.has_value()) {
            result.set_error_code("DB_ERROR");
            return result;
        }
        total_gold = total_opt.value();
    }

    if (!total_crystal.has_value() || !total_gold.has_value()) {
        auto data = game_repo_.get_user_game_data(user_id);
        if (!total_crystal.has_value()) {
            total_crystal = data.crystal;
        }
        if (!total_gold.has_value()) {
            total_gold = data.gold;
        }
    }

    result.set_success(true);
    result.set_chain_id(meta->chain_id);
    result.set_claimed_step(meta->step_index);
    result.set_reward_crystal(meta->reward_crystal);
    result.set_reward_gold(meta->reward_gold);
    result.set_total_crystal(total_crystal.value_or(0));
    result.set_total_gold(total_gold.value_or(0));
    result.set_error_code("");
    return result;
}

std::vector<infinitepickaxe::AchievementProgressUpdate> AchievementService::handle_mining_complete(
    const std::string& user_id) {
    return apply_progress_delta(user_id, {{"mine_any", 1}});
}

std::vector<infinitepickaxe::AchievementProgressUpdate> AchievementService::handle_upgrade_try(
    const std::string& user_id, bool success) {
    std::vector<std::pair<std::string, uint64_t>> deltas;
    deltas.emplace_back("upgrade_try", 1);
    if (success) {
        deltas.emplace_back("upgrade_success", 1);
    }
    return apply_progress_delta(user_id, deltas);
}

std::vector<infinitepickaxe::AchievementProgressUpdate> AchievementService::handle_gold_earned(
    const std::string& user_id, uint64_t gold_delta) {
    if (gold_delta == 0) {
        return {};
    }
    return apply_progress_delta(user_id, {{"gold", gold_delta}});
}

std::vector<infinitepickaxe::AchievementProgressUpdate> AchievementService::handle_play_time_seconds(
    const std::string& user_id, uint32_t seconds) {
    if (seconds == 0) {
        return {};
    }
    return apply_progress_delta(user_id, {{"play_time", seconds}});
}

std::vector<infinitepickaxe::AchievementProgressUpdate> AchievementService::handle_gem_created(
    const std::string& user_id, uint32_t created_count) {
    if (created_count == 0) {
        return {};
    }
    return apply_progress_delta(user_id, {{"gem_create", created_count}});
}

std::vector<infinitepickaxe::AchievementProgressUpdate> AchievementService::handle_gem_conversion(
    const std::string& user_id, uint32_t conversion_count) {
    if (conversion_count == 0) {
        return {};
    }
    return apply_progress_delta(user_id, {{"gem_conversion", conversion_count}});
}

std::vector<infinitepickaxe::AchievementProgressUpdate> AchievementService::handle_gem_synthesis(
    const std::string& user_id, uint32_t synthesis_count) {
    if (synthesis_count == 0) {
        return {};
    }
    return apply_progress_delta(user_id, {{"gem_synthesis", synthesis_count}});
}

std::vector<infinitepickaxe::AchievementProgressUpdate> AchievementService::handle_gem_discard(
    const std::string& user_id, uint32_t discard_count) {
    if (discard_count == 0) {
        return {};
    }
    return apply_progress_delta(user_id, {{"gem_discard", discard_count}});
}

std::vector<infinitepickaxe::AchievementProgressUpdate> AchievementService::apply_progress_delta(
    const std::string& user_id,
    const std::vector<std::pair<std::string, uint64_t>>& deltas) {
    std::vector<infinitepickaxe::AchievementProgressUpdate> updates;

    for (const auto& entry : deltas) {
        const auto& type = entry.first;
        uint64_t delta = entry.second;
        if (delta == 0 || type.empty()) {
            continue;
        }

        auto new_value = repo_.add_progress(user_id, type, delta);
        if (!new_value.has_value()) {
            spdlog::warn("achievement progress update failed: user={} type={}", user_id, type);
            continue;
        }

        infinitepickaxe::AchievementProgressUpdate update;
        update.set_achievement_type(type);
        update.set_current_value(new_value.value());
        updates.push_back(update);
    }

    return updates;
}
