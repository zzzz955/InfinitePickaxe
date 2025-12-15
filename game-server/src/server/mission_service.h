#pragma once
#include "game.pb.h"
#include "mission_repository.h"
#include "metadata/metadata_loader.h"
#include <string>
#include <vector>

class MissionService {
public:
    MissionService(MissionRepository& repo, const MetadataLoader& meta)
        : repo_(repo), meta_(meta) {}

    // 미션 목록 조회 (3개 슬롯)
    infinitepickaxe::DailyMissionsResponse get_missions(const std::string& user_id);

    // 미션 진행도 업데이트
    bool update_mission_progress(const std::string& user_id, uint32_t slot_no,
                                 uint32_t new_value);

    // 미션 완료 및 보상 수령
    infinitepickaxe::MissionCompleteResult claim_mission_reward(
        const std::string& user_id, uint32_t slot_no);

    // 미션 리롤
    infinitepickaxe::MissionRerollResult reroll_missions(const std::string& user_id);

    // 광고 카운터 조회
    std::vector<AdCounter> get_ad_counters(const std::string& user_id);

    // 광고 시청 처리
    bool process_ad_watch(const std::string& user_id, const std::string& ad_type);

private:
    // 새 미션 배정 (메타데이터에서 랜덤 선택)
    bool assign_random_mission(const std::string& user_id, uint32_t slot_no);

    MissionRepository& repo_;
    const MetadataLoader& meta_;
};
