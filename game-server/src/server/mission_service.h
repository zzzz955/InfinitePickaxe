#pragma once
#include "game.pb.h"
#include "mission_repository.h"
#include "metadata/metadata_loader.h"
#include "game_repository.h"
#include "offline_repository.h"
#include <string>
#include <vector>
#include <unordered_set>
#include <functional>
#include <optional>

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

    std::vector<infinitepickaxe::MissionProgressUpdate> handle_mining_complete(
        const std::string& user_id, uint32_t mineral_id);
    std::vector<infinitepickaxe::MissionProgressUpdate> handle_upgrade_try(
        const std::string& user_id, bool success);
    std::vector<infinitepickaxe::MissionProgressUpdate> handle_gold_earned(
        const std::string& user_id, uint64_t gold_delta);
    std::vector<infinitepickaxe::MissionProgressUpdate> handle_play_time_seconds(
        const std::string& user_id, uint32_t seconds);

private:
    DailyMissionInfo ensure_daily_state_kst(const std::string& user_id);
    bool assign_random_missions_unique(const std::string& user_id, uint32_t count);
    bool assign_random_mission(const std::string& user_id, uint32_t slot_no,
                               std::unordered_set<uint32_t>& used_meta_ids);
    const MissionMeta* get_mission_meta_by_id(uint32_t meta_id) const;
    const MissionMeta* get_mission_meta_for_slot(const MissionSlot& slot) const;
    std::vector<infinitepickaxe::MissionProgressUpdate> apply_progress_delta(
        const std::string& user_id,
        const std::function<uint64_t(const MissionSlot&, const MissionMeta*)>& delta_fn);
    std::optional<infinitepickaxe::MissionProgressUpdate> apply_progress_update(
        const std::string& user_id, const MissionSlot& slot, uint32_t new_value);

    MissionRepository& repo_;
    GameRepository& game_repo_;
    OfflineRepository& offline_repo_;
    const MetadataLoader& meta_;
};
