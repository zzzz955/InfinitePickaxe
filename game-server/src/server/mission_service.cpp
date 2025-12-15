#include "mission_service.h"
#include <spdlog/spdlog.h>
#include <random>
#include <chrono>

// 미션 목록 조회 (3개 슬롯)
infinitepickaxe::DailyMissionsResponse MissionService::get_missions(const std::string& user_id) {
    infinitepickaxe::DailyMissionsResponse response;

    // 일일 미션 정보 조회
    auto daily_info = repo_.get_or_create_daily_mission_info(user_id);
    response.set_assigned_count(daily_info.assigned_count);
    response.set_reroll_count(daily_info.reroll_count);

    // 미션 슬롯 조회 (최대 3개)
    auto slots = repo_.get_all_mission_slots(user_id);
    for (const auto& slot : slots) {
        auto* entry = response.add_missions();
        entry->set_slot_no(slot.slot_no);
        entry->set_mission_id(slot.mission_id);
        entry->set_mission_type(slot.mission_type);

        // 메타데이터에서 description 찾기
        std::string description = "미션";
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

        // timestamp를 Unix ms로 변환
        auto assigned_ms = std::chrono::duration_cast<std::chrono::milliseconds>(
            slot.assigned_at.time_since_epoch()).count();
        entry->set_assigned_at(assigned_ms);

        if (slot.expires_at.has_value()) {
            auto expires_ms = std::chrono::duration_cast<std::chrono::milliseconds>(
                slot.expires_at->time_since_epoch()).count();
            entry->set_expires_at(expires_ms);
        }
    }

    // 광고 카운터 추가
    auto ad_counters = repo_.get_all_ad_counters(user_id);
    for (const auto& counter : ad_counters) {
        auto* ad_counter = response.add_ad_counters();
        ad_counter->set_ad_type(counter.ad_type);
        ad_counter->set_ad_count(counter.ad_count);
        // daily_limit는 하드코딩 또는 설정에서 가져올 수 있음
        ad_counter->set_daily_limit(10);  // 예시 값
    }

    return response;
}

// 미션 진행도 업데이트
bool MissionService::update_mission_progress(const std::string& user_id, uint32_t slot_no,
                                             uint32_t new_value) {
    auto slot_opt = repo_.get_mission_slot(user_id, slot_no);
    if (!slot_opt.has_value()) {
        spdlog::warn("update_mission_progress: slot not found user={} slot={}", user_id, slot_no);
        return false;
    }

    auto& slot = slot_opt.value();

    // 이미 완료되었거나 클레임된 미션은 업데이트 불가
    if (slot.status == "completed" || slot.status == "claimed") {
        spdlog::debug("update_mission_progress: mission already done user={} slot={}", user_id, slot_no);
        return false;
    }

    // 목표치 달성 여부 확인
    std::string new_status = slot.status;
    if (new_value >= slot.target_value) {
        new_status = "completed";
    }

    bool success = repo_.update_mission_progress(user_id, slot_no, new_value, new_status);

    if (success && new_status == "completed") {
        repo_.complete_mission(user_id, slot_no);
    }

    return success;
}

// 미션 완료 및 보상 수령
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

    // 완료 상태 확인
    if (slot.status != "completed") {
        result.set_error_code("MISSION_NOT_COMPLETED");
        return result;
    }

    // 이미 클레임했는지 확인
    if (slot.status == "claimed") {
        result.set_error_code("ALREADY_CLAIMED");
        return result;
    }

    // 보상 지급 (여기서는 DB 업데이트만, 실제 크리스탈 지급은 상위 레이어에서)
    bool claimed = repo_.claim_mission_reward(user_id, slot_no);
    if (!claimed) {
        result.set_error_code("DB_ERROR");
        return result;
    }

    result.set_success(true);
    result.set_mission_id(slot.mission_id);
    result.set_reward_crystal(slot.reward_crystal);
    // total_crystal은 호출자가 설정

    spdlog::debug("claim_mission_reward: user={} slot={} reward={}",
                  user_id, slot_no, slot.reward_crystal);

    return result;
}

// 미션 리롤
infinitepickaxe::MissionRerollResult MissionService::reroll_missions(const std::string& user_id) {
    infinitepickaxe::MissionRerollResult result;
    result.set_success(false);

    // 일일 미션 정보 조회
    auto daily_info = repo_.get_or_create_daily_mission_info(user_id);

    // 리롤 제한 확인 (예: 하루 3회)
    const uint32_t MAX_REROLLS = 3;
    if (daily_info.reroll_count >= MAX_REROLLS) {
        result.set_error_code("REROLL_LIMIT_EXCEEDED");
        return result;
    }

    // 기존 미션 슬롯 삭제
    for (uint32_t slot_no = 1; slot_no <= 3; ++slot_no) {
        repo_.delete_mission_slot(user_id, slot_no);
    }

    // 새 미션 배정
    for (uint32_t slot_no = 1; slot_no <= 3; ++slot_no) {
        if (!assign_random_mission(user_id, slot_no)) {
            spdlog::error("reroll_missions: failed to assign slot={}", slot_no);
        }
    }

    // 리롤 카운트 증가
    repo_.increment_reroll_count(user_id);

    // 새 미션 목록 반환
    auto slots = repo_.get_all_mission_slots(user_id);
    for (const auto& slot : slots) {
        auto* entry = result.add_rerolled_missions();
        entry->set_slot_no(slot.slot_no);
        entry->set_mission_id(slot.mission_id);
        entry->set_mission_type(slot.mission_type);

        std::string description = "미션";
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

// 광고 카운터 조회
std::vector<AdCounter> MissionService::get_ad_counters(const std::string& user_id) {
    return repo_.get_all_ad_counters(user_id);
}

// 광고 시청 처리
bool MissionService::process_ad_watch(const std::string& user_id, const std::string& ad_type) {
    // 광고 카운터 증가
    bool success = repo_.increment_ad_counter(user_id, ad_type);

    if (success) {
        spdlog::debug("process_ad_watch: user={} ad_type={}", user_id, ad_type);
    }

    return success;
}

// private: 새 미션 배정 (메타데이터에서 랜덤 선택)
bool MissionService::assign_random_mission(const std::string& user_id, uint32_t slot_no) {
    const auto& missions = meta_.missions();
    if (missions.empty()) {
        spdlog::error("assign_random_mission: no missions in metadata");
        return false;
    }

    // 랜덤 미션 선택
    static std::mt19937 rng(std::random_device{}());
    std::uniform_int_distribution<size_t> dist(0, missions.size() - 1);
    size_t idx = dist(rng);

    const auto& mission = missions[idx];

    // 미션 배정
    bool success = repo_.assign_mission_to_slot(
        user_id, slot_no, mission.type, mission.target, mission.reward_crystal);

    if (success) {
        // 일일 배정 카운트 증가
        repo_.increment_assigned_count(user_id, 1);
        spdlog::debug("assign_random_mission: user={} slot={} type={} target={}",
                      user_id, slot_no, mission.type, mission.target);
    }

    return success;
}
