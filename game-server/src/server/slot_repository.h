#pragma once
#include "connection_pool.h"
#include <string>
#include <vector>
#include <optional>

// 슬롯 데이터 구조체
struct PickaxeSlot {
    std::string slot_id;
    std::string user_id;
    uint32_t slot_index;      // 0-3
    uint32_t level;           // 곡괭이 레벨
    uint32_t tier;            // 티어 (1-5)
    uint64_t attack_power;    // 공격력
    uint32_t attack_speed_x100; // 공격속도 * 100
    uint64_t dps;             // DPS = attack_power * attack_speed_x100 / 100
    uint32_t pity_bonus;      // 천장 보너스 (basis point)
};

class SlotRepository {
public:
    explicit SlotRepository(ConnectionPool& pool) : pool_(pool) {}

    // 유저의 모든 슬롯 조회 (최대 4개)
    std::vector<PickaxeSlot> get_user_slots(const std::string& user_id);

    // 특정 슬롯 조회
    std::optional<PickaxeSlot> get_slot(const std::string& user_id, uint32_t slot_index);

    // 슬롯 생성 (초기화 시)
    bool create_slot(const std::string& user_id, uint32_t slot_index,
                     uint32_t level, uint32_t tier,
                     uint64_t attack_power, uint32_t attack_speed_x100, uint64_t dps);

    // 슬롯 업데이트 (레벨업, 강화 시)
    bool update_slot(const std::string& user_id, uint32_t slot_index,
                     uint32_t new_level, uint32_t new_tier,
                     uint64_t new_attack_power, uint32_t new_attack_speed_x100,
                     uint64_t new_dps, uint32_t new_pity_bonus);

private:
    ConnectionPool& pool_;
};
