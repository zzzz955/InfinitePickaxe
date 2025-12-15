#pragma once
#include "connection_pool.h"
#include <string>
#include <vector>
#include <optional>
#include <chrono>

// 광고 카운터 구조체 (user_ad_counters 테이블)
struct AdCounter {
    std::string user_id;
    std::string ad_type;  // "upgrade_discount", "mission_reroll", "crystal_reward"
    uint32_t ad_count;
    std::chrono::system_clock::time_point reset_date;
};

// 일일 미션 정보 구조체 (user_mission_daily 테이블)
struct DailyMissionInfo {
    std::string user_id;
    std::chrono::system_clock::time_point mission_date;
    uint32_t completed_count; // 오늘 완료된 미션 수
    uint32_t reroll_count;    // 오늘 리롤 사용 횟수
};

// 미션 슬롯 구조체 (user_mission_slots 테이블)
struct MissionSlot {
    std::string user_id;
    uint32_t slot_no;                    // 1-3
    std::string mission_id;              // UUID
    std::string mission_type;            // "mine", "play_time", "upgrade", "gold", "level" 등
    uint32_t target_value;
    uint32_t current_value;
    uint32_t reward_crystal;
    std::string status;                  // "active", "completed", "claimed"
    std::chrono::system_clock::time_point assigned_at;
    std::optional<std::chrono::system_clock::time_point> completed_at;
    std::optional<std::chrono::system_clock::time_point> claimed_at;
    std::optional<std::chrono::system_clock::time_point> expires_at;
};

class MissionRepository {
public:
    explicit MissionRepository(ConnectionPool& pool) : pool_(pool) {}

    // === 광고 카운터 관련 ===
    // 특정 ad_type의 카운터 조회 (없으면 생성)
    AdCounter get_or_create_ad_counter(const std::string& user_id, const std::string& ad_type);

    // 광고 카운터 증가
    bool increment_ad_counter(const std::string& user_id, const std::string& ad_type);

    // 모든 광고 카운터 조회
    std::vector<AdCounter> get_all_ad_counters(const std::string& user_id);

    // === 일일 미션 정보 관련 ===
    // 오늘 날짜의 일일 미션 정보 조회 (없으면 생성)
    DailyMissionInfo get_or_create_daily_mission_info(const std::string& user_id);

    // 완료 카운트 증가
    bool increment_completed_count(const std::string& user_id, uint32_t count = 1);

    // 리롤 카운트 증가
    bool increment_reroll_count(const std::string& user_id);

    // === 마일스톤 청구 관련 ===
    bool has_milestone_claimed(const std::string& user_id, uint32_t milestone_count);
    bool insert_milestone_claim(const std::string& user_id, uint32_t milestone_count);

    // === 미션 슬롯 관련 ===
    // 특정 슬롯 조회
    std::optional<MissionSlot> get_mission_slot(const std::string& user_id, uint32_t slot_no);

    // 모든 슬롯 조회 (최대 3개)
    std::vector<MissionSlot> get_all_mission_slots(const std::string& user_id);

    // 미션 슬롯 생성/배정
    bool assign_mission_to_slot(const std::string& user_id, uint32_t slot_no,
                                const std::string& mission_type, uint32_t target_value,
                                uint32_t reward_crystal);

    // 미션 진행도 업데이트
    bool update_mission_progress(const std::string& user_id, uint32_t slot_no,
                                 uint32_t new_current_value, const std::string& new_status);

    // 미션 완료 처리 (상태 변경)
    bool complete_mission(const std::string& user_id, uint32_t slot_no);

    // 미션 보상 수령 (claimed 상태로 변경)
    bool claim_mission_reward(const std::string& user_id, uint32_t slot_no);

    // 미션 슬롯 삭제 (리롤 시)
    bool delete_mission_slot(const std::string& user_id, uint32_t slot_no);

private:
    ConnectionPool& pool_;
};
