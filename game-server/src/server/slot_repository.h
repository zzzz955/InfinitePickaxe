#pragma once
#include "connection_pool.h"
#include <optional>
#include <string>
#include <vector>

struct PickaxeSlot {
    std::string slot_id;
    std::string user_id;
    uint32_t slot_index;           // 0-3
    uint32_t level;                // 0-109
    uint32_t tier;                 // 1-22
    uint64_t attack_power;
    uint32_t attack_speed_x100;    // *100 (max 2500)
    uint32_t critical_hit_percent; // *10000 (0-10000)
    uint32_t critical_damage;      // *100 (15000 = 150%)
    uint64_t dps;
    uint32_t pity_bonus;           // 0-10000
};

struct SlotUnlockDBResult {
    bool success{false};
    bool already_unlocked{false};
    bool insufficient_crystal{false};
    uint32_t remaining_crystal{0};
    uint64_t total_dps{0};
};

class SlotRepository {
public:
    explicit SlotRepository(ConnectionPool& pool) : pool_(pool) {}

    std::vector<PickaxeSlot> get_user_slots(const std::string& user_id);

    std::optional<PickaxeSlot> get_slot(const std::string& user_id, uint32_t slot_index);

    bool create_slot(const std::string& user_id, uint32_t slot_index,
                     uint32_t level, uint32_t tier,
                     uint64_t attack_power, uint32_t attack_speed_x100,
                     uint32_t critical_hit_percent, uint32_t critical_damage,
                     uint64_t dps);

    bool update_slot(const std::string& user_id, uint32_t slot_index,
                     uint32_t new_level, uint32_t new_tier,
                     uint64_t new_attack_power, uint32_t new_attack_speed_x100,
                     uint32_t new_critical_hit_percent, uint32_t new_critical_damage,
                     uint64_t new_dps, uint32_t new_pity_bonus);

    SlotUnlockDBResult create_and_unlock_slot(const PickaxeSlot& slot, uint32_t crystal_cost);

private:
    ConnectionPool& pool_;
};
