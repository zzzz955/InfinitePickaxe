#include "infinite_mine_service.h"
#include "time_utils.h"
#include <spdlog/spdlog.h>
#include <algorithm>
#include <chrono>
#include <ctime>
#include <limits>

namespace {
uint32_t clamp_u64_to_u32(uint64_t value) {
    if (value > std::numeric_limits<uint32_t>::max()) {
        return std::numeric_limits<uint32_t>::max();
    }
    return static_cast<uint32_t>(value);
}
}

std::string InfiniteMineService::kst_today_date_string() const {
    using namespace std::chrono;
    auto now_kst = system_clock::now() + hours(9);
    std::time_t tt = system_clock::to_time_t(now_kst);
    std::tm tm = *std::gmtime(&tt);
    char buf[11];
    std::strftime(buf, sizeof(buf), "%Y-%m-%d", &tm);
    return std::string(buf);
}

uint32_t InfiniteMineService::resolve_auto_reward_divisor() const {
    uint32_t divisor = meta_.infinite_mine_config().auto_reward_divisor;
    return divisor == 0 ? 1 : divisor;
}

bool InfiniteMineService::is_auto_claimable(const InfiniteMineProgress& progress, const std::string& today) const {
    if (progress.first_cleared_date == today) {
        return false;
    }
    if (!progress.last_auto_claim_date.empty() && progress.last_auto_claim_date == today) {
        return false;
    }
    return true;
}

uint32_t InfiniteMineService::get_highest_cleared_floor(const std::string& user_id) {
    return repo_.get_highest_cleared_floor(user_id);
}

infinitepickaxe::InfiniteMineStateResponse InfiniteMineService::get_state(const std::string& user_id) {
    infinitepickaxe::InfiniteMineStateResponse response;
    const auto& config = meta_.infinite_mine_config();
    response.set_reset_timestamp_ms(kst_next_midnight_ms());
    response.set_time_limit_sec(config.time_limit_sec);
    response.set_max_floor(config.max_floor);

    auto progress = repo_.get_all_progress(user_id);
    uint32_t highest = 0;
    const std::string today = kst_today_date_string();

    for (const auto& entry : progress) {
        if (entry.floor > highest) {
            highest = entry.floor;
        }
        auto* state = response.add_floor_states();
        state->set_floor(entry.floor);
        const bool claimed_today = !entry.last_auto_claim_date.empty() && entry.last_auto_claim_date == today;
        state->set_auto_claimed_today(claimed_today);
        state->set_auto_claimable(is_auto_claimable(entry, today));
    }

    response.set_highest_cleared_floor(highest);
    return response;
}

infinitepickaxe::InfiniteMineChallengeStartResult InfiniteMineService::start_challenge(
    const std::string& user_id, uint32_t floor) {

    infinitepickaxe::InfiniteMineChallengeStartResult result;
    result.set_success(false);
    result.set_floor(floor);

    const auto& config = meta_.infinite_mine_config();
    if (floor == 0 || floor > config.max_floor) {
        result.set_error_code("INVALID_FLOOR");
        return result;
    }

    const auto* floor_meta = meta_.infinite_mine_floor(floor);
    if (!floor_meta) {
        result.set_error_code("INVALID_FLOOR");
        return result;
    }

    const uint32_t highest_cleared = repo_.get_highest_cleared_floor(user_id);
    if (floor <= highest_cleared) {
        result.set_error_code("ALREADY_CLEARED");
        return result;
    }
    if (floor > highest_cleared + 1) {
        result.set_error_code("FLOOR_LOCKED");
        return result;
    }

    result.set_success(true);
    result.set_current_hp(floor_meta->hp);
    result.set_max_hp(floor_meta->hp);
    result.set_time_limit_sec(config.time_limit_sec);
    result.set_remaining_ms(static_cast<uint64_t>(config.time_limit_sec) * 1000ULL);
    result.set_error_code("");
    return result;
}

infinitepickaxe::InfiniteMineChallengeResult InfiniteMineService::handle_clear(
    const std::string& user_id, uint32_t floor) {

    infinitepickaxe::InfiniteMineChallengeResult result;
    result.set_success(false);
    result.set_floor(floor);
    result.set_reason(infinitepickaxe::CLEARED);

    const auto* floor_meta = meta_.infinite_mine_floor(floor);
    if (!floor_meta) {
        result.set_reason(infinitepickaxe::INFINITE_MINE_RESULT_UNKNOWN);
        return result;
    }

    auto inserted = repo_.insert_first_clear(user_id, floor);
    if (!inserted.has_value()) {
        result.set_reason(infinitepickaxe::INFINITE_MINE_RESULT_UNKNOWN);
        return result;
    }
    if (!inserted.value()) {
        spdlog::warn("infinite mine clear already exists: user={} floor={}", user_id, floor);
        result.set_reason(infinitepickaxe::INFINITE_MINE_RESULT_UNKNOWN);
        return result;
    }

    const uint64_t reward_gold = floor_meta->reward_gold;
    const uint32_t reward_crystal = clamp_u64_to_u32(floor_meta->reward_crystal);

    std::optional<uint64_t> total_gold;
    std::optional<uint32_t> total_crystal;

    if (reward_gold > 0) {
        auto total_opt = game_repo_.add_gold(user_id, reward_gold);
        if (!total_opt.has_value()) {
            result.set_reason(infinitepickaxe::INFINITE_MINE_RESULT_UNKNOWN);
            return result;
        }
        total_gold = total_opt.value();
    }

    if (reward_crystal > 0) {
        auto total_opt = game_repo_.add_crystal(user_id, reward_crystal);
        if (!total_opt.has_value()) {
            result.set_reason(infinitepickaxe::INFINITE_MINE_RESULT_UNKNOWN);
            return result;
        }
        total_crystal = total_opt.value();
    }

    if (!total_gold.has_value() || !total_crystal.has_value()) {
        auto data = game_repo_.get_user_game_data(user_id);
        if (!total_gold.has_value()) {
            total_gold = data.gold;
        }
        if (!total_crystal.has_value()) {
            total_crystal = data.crystal;
        }
    }

    result.set_success(true);
    result.set_reward_gold(reward_gold);
    result.set_reward_crystal(reward_crystal);
    result.set_total_gold(total_gold.value_or(0));
    result.set_total_crystal(total_crystal.value_or(0));
    return result;
}

infinitepickaxe::InfiniteMineAutoClaimResult InfiniteMineService::claim_auto_reward(
    const std::string& user_id, uint32_t floor) {

    infinitepickaxe::InfiniteMineAutoClaimResult result;
    result.set_success(false);
    result.set_floor(floor);

    const auto& config = meta_.infinite_mine_config();
    if (floor == 0 || floor > config.max_floor) {
        result.set_error_code("INVALID_FLOOR");
        return result;
    }

    const auto* floor_meta = meta_.infinite_mine_floor(floor);
    if (!floor_meta) {
        result.set_error_code("INVALID_FLOOR");
        return result;
    }

    auto progress = repo_.get_progress(user_id, floor);
    if (!progress.has_value()) {
        result.set_error_code("NOT_CLEARED");
        return result;
    }

    const std::string today = kst_today_date_string();
    if (progress->first_cleared_date == today) {
        result.set_error_code("FIRST_CLEAR_TODAY");
        return result;
    }
    if (!progress->last_auto_claim_date.empty() && progress->last_auto_claim_date == today) {
        result.set_error_code("ALREADY_CLAIMED");
        return result;
    }

    if (!repo_.update_auto_claim_date(user_id, floor, today)) {
        result.set_error_code("DB_ERROR");
        return result;
    }

    const uint32_t divisor = resolve_auto_reward_divisor();
    const uint64_t reward_gold = floor_meta->reward_gold / divisor;
    const uint32_t reward_crystal = clamp_u64_to_u32(floor_meta->reward_crystal / divisor);

    std::optional<uint64_t> total_gold;
    std::optional<uint32_t> total_crystal;

    if (reward_gold > 0) {
        auto total_opt = game_repo_.add_gold(user_id, reward_gold);
        if (!total_opt.has_value()) {
            result.set_error_code("DB_ERROR");
            return result;
        }
        total_gold = total_opt.value();
    }

    if (reward_crystal > 0) {
        auto total_opt = game_repo_.add_crystal(user_id, reward_crystal);
        if (!total_opt.has_value()) {
            result.set_error_code("DB_ERROR");
            return result;
        }
        total_crystal = total_opt.value();
    }

    if (!total_gold.has_value() || !total_crystal.has_value()) {
        auto data = game_repo_.get_user_game_data(user_id);
        if (!total_gold.has_value()) {
            total_gold = data.gold;
        }
        if (!total_crystal.has_value()) {
            total_crystal = data.crystal;
        }
    }

    result.set_success(true);
    result.set_reward_gold(reward_gold);
    result.set_reward_crystal(reward_crystal);
    result.set_total_gold(total_gold.value_or(0));
    result.set_total_crystal(total_crystal.value_or(0));
    result.set_error_code("");
    return result;
}

infinitepickaxe::InfiniteMineAutoClaimAllResult InfiniteMineService::claim_all_auto_rewards(
    const std::string& user_id) {

    infinitepickaxe::InfiniteMineAutoClaimAllResult result;
    result.set_success(false);

    auto progress = repo_.get_all_progress(user_id);
    if (progress.empty()) {
        result.set_error_code("NOTHING_TO_CLAIM");
        return result;
    }

    const std::string today = kst_today_date_string();
    const uint32_t divisor = resolve_auto_reward_divisor();

    std::vector<uint32_t> claimable_floors;
    uint64_t total_reward_gold = 0;
    uint64_t total_reward_crystal_raw = 0;

    for (const auto& entry : progress) {
        if (!is_auto_claimable(entry, today)) {
            continue;
        }
        const auto* floor_meta = meta_.infinite_mine_floor(entry.floor);
        if (!floor_meta) {
            continue;
        }
        claimable_floors.push_back(entry.floor);
        total_reward_gold += floor_meta->reward_gold / divisor;
        total_reward_crystal_raw += floor_meta->reward_crystal / divisor;
    }

    if (claimable_floors.empty()) {
        result.set_error_code("NOTHING_TO_CLAIM");
        return result;
    }

    if (!repo_.update_auto_claim_dates(user_id, claimable_floors, today)) {
        result.set_error_code("DB_ERROR");
        return result;
    }

    const uint32_t total_reward_crystal = clamp_u64_to_u32(total_reward_crystal_raw);

    std::optional<uint64_t> total_gold;
    std::optional<uint32_t> total_crystal;

    if (total_reward_gold > 0) {
        auto total_opt = game_repo_.add_gold(user_id, total_reward_gold);
        if (!total_opt.has_value()) {
            result.set_error_code("DB_ERROR");
            return result;
        }
        total_gold = total_opt.value();
    }

    if (total_reward_crystal > 0) {
        auto total_opt = game_repo_.add_crystal(user_id, total_reward_crystal);
        if (!total_opt.has_value()) {
            result.set_error_code("DB_ERROR");
            return result;
        }
        total_crystal = total_opt.value();
    }

    if (!total_gold.has_value() || !total_crystal.has_value()) {
        auto data = game_repo_.get_user_game_data(user_id);
        if (!total_gold.has_value()) {
            total_gold = data.gold;
        }
        if (!total_crystal.has_value()) {
            total_crystal = data.crystal;
        }
    }

    result.set_success(true);
    result.set_total_reward_gold(total_reward_gold);
    result.set_total_reward_crystal(total_reward_crystal);
    result.set_total_gold(total_gold.value_or(0));
    result.set_total_crystal(total_crystal.value_or(0));
    result.set_error_code("");
    return result;
}
