#pragma once
#include "game.pb.h"
#include "mission_repository.h"
#include "metadata/metadata_loader.h"
#include "game_repository.h"
#include "offline_repository.h"
#include <string>
#include <vector>

class MissionService {
public:
    MissionService(MissionRepository& repo, GameRepository& game_repo, OfflineRepository& offline_repo, const MetadataLoader& meta)
        : repo_(repo), game_repo_(game_repo), offline_repo_(offline_repo), meta_(meta) {}

    infinitepickaxe::DailyMissionsResponse get_missions(const std::string& user_id);

    bool update_mission_progress(const std::string& user_id, uint32_t slot_no,
                                 uint32_t new_value);

    infinitepickaxe::MissionCompleteResult claim_mission_reward(
        const std::string& user_id, uint32_t slot_no);

    infinitepickaxe::MissionRerollResult reroll_missions(const std::string& user_id);

    std::vector<AdCounter> get_ad_counters(const std::string& user_id);

    infinitepickaxe::AdWatchResult handle_ad_watch(const std::string& user_id, const std::string& ad_type);

    infinitepickaxe::MilestoneClaimResult handle_milestone_claim(
        const std::string& user_id, uint32_t milestone_count);

private:
    bool assign_random_mission(const std::string& user_id, uint32_t slot_no);

    MissionRepository& repo_;
    GameRepository& game_repo_;
    OfflineRepository& offline_repo_;
    const MetadataLoader& meta_;
};
