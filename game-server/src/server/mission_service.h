#pragma once
#include "game.pb.h"
#include "mission_repository.h"
#include "metadata/metadata_loader.h"
#include "game_repository.h"
#include "offline_repository.h"
#include "redis_client.h"
#include <string>
#include <vector>
#include <unordered_set>
#include <functional>
#include <optional>

class AdService;

class MissionService {
public:
    MissionService(MissionRepository& repo, GameRepository& game_repo, OfflineRepository& offline_repo,
                   AdService& ad_service, const MetadataLoader& meta, RedisClient& redis)
        : repo_(repo), game_repo_(game_repo), offline_repo_(offline_repo),
          ad_service_(ad_service), meta_(meta), redis_(redis) {}

    infinitepickaxe::DailyMissionsResponse get_missions(const std::string& user_id);

    infinitepickaxe::MissionCompleteResult claim_mission_reward(
        const std::string& user_id, uint32_t slot_no);

    infinitepickaxe::MissionRerollResult reroll_missions(const std::string& user_id);
    infinitepickaxe::MissionRerollResult reroll_missions_ad(const std::string& user_id);

    infinitepickaxe::MilestoneClaimResult handle_milestone_claim(
        const std::string& user_id, uint32_t milestone_count);
    infinitepickaxe::MilestoneState get_milestone_state(const std::string& user_id);

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
    infinitepickaxe::MissionRerollResult reroll_missions_internal(const std::string& user_id, bool use_ad);
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
    std::vector<MissionSlot> load_cached_slots(const std::string& user_id);
    std::optional<MissionSlot> load_cached_slot(const std::string& user_id, uint32_t slot_no);
    void cache_slot(const std::string& user_id, const MissionSlot& slot);
    void cache_slots(const std::string& user_id, const std::vector<MissionSlot>& slots);
    void update_slot_cache(const std::string& user_id, const MissionSlot& slot);
    bool flush_slots_if_due(const std::string& user_id, const std::vector<MissionSlot>& slots);
    void flush_slots_to_db(const std::string& user_id, const std::vector<MissionSlot>& slots);
    void flush_slot_to_db(const std::string& user_id, const MissionSlot& slot);

    MissionRepository& repo_;
    GameRepository& game_repo_;
    OfflineRepository& offline_repo_;
    AdService& ad_service_;
    const MetadataLoader& meta_;
    RedisClient& redis_;
};
