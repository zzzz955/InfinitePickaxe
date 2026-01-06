#pragma once
#include "achievement_repository.h"
#include "game.pb.h"
#include "game_repository.h"
#include "metadata/metadata_loader.h"
#include <string>
#include <vector>
#include <optional>
#include <utility>

class AchievementService {
public:
    AchievementService(AchievementRepository& repo, GameRepository& game_repo,
                       const MetadataLoader& meta)
        : repo_(repo), game_repo_(game_repo), meta_(meta) {}

    infinitepickaxe::AchievementsResponse get_state(const std::string& user_id);
    infinitepickaxe::AchievementClaimResult claim_achievement(const std::string& user_id,
                                                              uint32_t achievement_id);

    std::vector<infinitepickaxe::AchievementProgressUpdate> handle_mining_complete(
        const std::string& user_id);
    std::vector<infinitepickaxe::AchievementProgressUpdate> handle_upgrade_try(
        const std::string& user_id, bool success, bool count_fail);
    std::vector<infinitepickaxe::AchievementProgressUpdate> handle_gold_earned(
        const std::string& user_id, uint64_t gold_delta);
    std::vector<infinitepickaxe::AchievementProgressUpdate> handle_play_time_seconds(
        const std::string& user_id, uint32_t seconds);
    std::vector<infinitepickaxe::AchievementProgressUpdate> handle_gem_created(
        const std::string& user_id, uint32_t created_count);
    std::vector<infinitepickaxe::AchievementProgressUpdate> handle_gem_conversion(
        const std::string& user_id, uint32_t conversion_count);
    std::vector<infinitepickaxe::AchievementProgressUpdate> handle_gem_synthesis(
        const std::string& user_id, uint32_t attempt_count, uint32_t success_count);
    std::vector<infinitepickaxe::AchievementProgressUpdate> handle_gem_discard(
        const std::string& user_id, uint32_t discard_count);

private:
    std::vector<infinitepickaxe::AchievementProgressUpdate> apply_progress_delta(
        const std::string& user_id,
        const std::vector<std::pair<std::string, uint64_t>>& deltas);

    AchievementRepository& repo_;
    GameRepository& game_repo_;
    const MetadataLoader& meta_;
};
