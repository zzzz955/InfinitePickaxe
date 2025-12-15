#include "slot_service.h"
#include <spdlog/spdlog.h>

namespace {
constexpr uint32_t kDefaultAttackSpeedX100 = 100;   // 1.0 APS
constexpr uint32_t kDefaultCritPercent = 500;       // 5%
constexpr uint32_t kDefaultCritDamage = 15000;      // 150%
constexpr uint32_t kDefaultPity = 0;
}

infinitepickaxe::AllSlotsResponse SlotService::handle_all_slots(const std::string& user_id) const {
    infinitepickaxe::AllSlotsResponse response;

    auto slots = repo_.get_user_slots(user_id);

    for (const auto& slot : slots) {
        auto* slot_info = response.add_slots();
        slot_info->set_slot_index(slot.slot_index);
        slot_info->set_level(slot.level);
        slot_info->set_tier(slot.tier);
        slot_info->set_attack_power(slot.attack_power);
        slot_info->set_attack_speed_x100(slot.attack_speed_x100);
        slot_info->set_critical_hit_percent(slot.critical_hit_percent);
        slot_info->set_critical_damage(slot.critical_damage);
        slot_info->set_pity_bonus(slot.pity_bonus);
        slot_info->set_dps(slot.dps);
        slot_info->set_is_unlocked(true);
    }

    uint64_t total_dps = calculate_total_dps(slots);
    response.set_total_dps(total_dps);

    spdlog::debug("handle_all_slots: user={} slots={} total_dps={}", user_id, slots.size(), total_dps);
    return response;
}

infinitepickaxe::SlotUnlockResult SlotService::handle_unlock(const std::string& user_id, uint32_t slot_index) const {
    infinitepickaxe::SlotUnlockResult res;
    res.set_slot_index(slot_index);

    if (slot_index < 1 || slot_index > 3) {
        res.set_success(false);
        res.set_error_code("INVALID_SLOT_INDEX");
        return res;
    }

    if (repo_.get_slot(user_id, slot_index).has_value()) {
        res.set_success(false);
        res.set_error_code("ALREADY_UNLOCKED");
        return res;
    }

    const auto* next_level_meta = meta_.pickaxe_level(1);
    const auto* base_level_meta = meta_.pickaxe_level(0);
    if (!next_level_meta) {
        res.set_success(false);
        res.set_error_code("MISSING_PICKAXE_META");
        return res;
    }
    uint32_t crystal_cost = static_cast<uint32_t>(next_level_meta->cost);

    PickaxeSlot slot{};
    slot.user_id = user_id;
    slot.slot_index = slot_index;
    slot.level = 0;
    slot.tier = base_level_meta ? base_level_meta->tier : next_level_meta->tier;
    slot.attack_power = base_level_meta ? base_level_meta->attack_power : next_level_meta->attack_power;
    slot.attack_speed_x100 = kDefaultAttackSpeedX100;
    slot.critical_hit_percent = kDefaultCritPercent;
    slot.critical_damage = kDefaultCritDamage;
    slot.dps = base_level_meta ? base_level_meta->dps : next_level_meta->dps;
    slot.pity_bonus = kDefaultPity;

    auto db_res = repo_.create_and_unlock_slot(slot, crystal_cost);
    if (db_res.already_unlocked) {
        res.set_success(false);
        res.set_error_code("ALREADY_UNLOCKED");
        return res;
    }
    if (db_res.insufficient_crystal) {
        res.set_success(false);
        res.set_error_code("INSUFFICIENT_CRYSTAL");
        res.set_remaining_crystal(db_res.remaining_crystal);
        res.set_crystal_spent(0);
        return res;
    }
    if (!db_res.success) {
        res.set_success(false);
        res.set_error_code("DB_ERROR");
        return res;
    }

    res.set_success(true);
    res.set_error_code("");
    res.set_crystal_spent(crystal_cost);
    res.set_remaining_crystal(db_res.remaining_crystal);
    return res;
}

uint64_t SlotService::calculate_total_dps(const std::vector<PickaxeSlot>& slots) const {
    uint64_t total = 0;
    for (const auto& slot : slots) {
        total += slot.dps;
    }
    return total;
}
