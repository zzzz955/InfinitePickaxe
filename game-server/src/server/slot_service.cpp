#include "slot_service.h"
#include <spdlog/spdlog.h>
#include <array>
#include <cmath>

namespace {
constexpr uint32_t kDefaultAttackSpeedX100 = 100;   // 1.0 APS
constexpr uint32_t kDefaultCritPercent = 500;       // 5%
constexpr uint32_t kDefaultCritDamage = 15000;      // 150%
constexpr uint32_t kDefaultPity = 0;
constexpr std::array<uint32_t, 4> kSlotCrystalCosts = {0, 400, 2000, 4000};

std::optional<uint32_t> crystal_cost_for_slot(uint32_t slot_index)
{
    if (slot_index >= kSlotCrystalCosts.size())
    {
        return std::nullopt;
    }
    return kSlotCrystalCosts[slot_index];
}

uint64_t compute_expected_dps(uint64_t attack_power, uint32_t attack_speed_x100,
                              uint32_t crit_percent, uint32_t crit_damage,
                              uint64_t fallback_dps)
{
    double attack_speed = static_cast<double>(attack_speed_x100) / 100.0;
    double crit_rate = static_cast<double>(crit_percent) / 10000.0;
    double crit_mult = static_cast<double>(crit_damage) / 10000.0;
    double expected = static_cast<double>(attack_power) * attack_speed *
                      (1.0 + crit_rate * (crit_mult - 1.0));
    uint64_t dps = static_cast<uint64_t>(std::llround(expected));
    if (dps == 0)
    {
        dps = fallback_dps;
    }
    return dps;
}

PickaxeSlot build_base_slot(const std::string& user_id, uint32_t slot_index, const MetadataLoader& meta)
{
    PickaxeSlot slot{};
    slot.user_id = user_id;
    slot.slot_index = slot_index;
    slot.level = 0;
    slot.tier = 1;
    slot.attack_power = 10;
    slot.attack_speed_x100 = kDefaultAttackSpeedX100;
    slot.critical_hit_percent = kDefaultCritPercent;
    slot.critical_damage = kDefaultCritDamage;
    slot.pity_bonus = kDefaultPity;

    if (const auto* base = meta.pickaxe_level(0))
    {
        slot.level = base->level;
        slot.tier = base->tier;
        slot.attack_power = base->attack_power;
        slot.attack_speed_x100 = static_cast<uint32_t>(std::lround(base->attack_speed * 100.0));
        if (slot.attack_speed_x100 == 0)
        {
            slot.attack_speed_x100 = 1;
        }
        slot.dps = compute_expected_dps(slot.attack_power, slot.attack_speed_x100,
                                        slot.critical_hit_percent, slot.critical_damage,
                                        base->dps);
    }
    else
    {
        slot.dps = compute_expected_dps(slot.attack_power, slot.attack_speed_x100,
                                        slot.critical_hit_percent, slot.critical_damage,
                                        slot.attack_power);
    }
    return slot;
}

void fill_slot_info(const PickaxeSlot& slot, infinitepickaxe::PickaxeSlotInfo* slot_info)
{
    if (!slot_info)
    {
        return;
    }
    slot_info->set_slot_index(slot.slot_index);
    slot_info->set_level(slot.level);
    slot_info->set_tier(slot.tier);
    slot_info->set_attack_power(slot.attack_power);
    slot_info->set_attack_speed_x100(slot.attack_speed_x100);
    slot_info->set_critical_hit_percent(slot.critical_hit_percent);
    slot_info->set_critical_damage(slot.critical_damage);
    slot_info->set_dps(slot.dps);
    slot_info->set_pity_bonus(slot.pity_bonus);
    slot_info->set_is_unlocked(true);
}
}

infinitepickaxe::AllSlotsResponse SlotService::handle_all_slots(const std::string& user_id) const {
    infinitepickaxe::AllSlotsResponse response;

    auto slots = repo_.get_user_slots(user_id);

    for (const auto& slot : slots) {
        fill_slot_info(slot, response.add_slots());
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

    auto crystal_cost = crystal_cost_for_slot(slot_index);
    if (!crystal_cost.has_value()) {
        res.set_success(false);
        res.set_error_code("INVALID_SLOT_INDEX");
        return res;
    }

    PickaxeSlot slot = build_base_slot(user_id, slot_index, meta_);

    auto db_res = repo_.create_and_unlock_slot(slot, *crystal_cost);
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
    res.set_crystal_spent(*crystal_cost);
    res.set_remaining_crystal(db_res.remaining_crystal);
    res.set_total_dps(db_res.total_dps);
    fill_slot_info(slot, res.mutable_new_slot());
    return res;
}

uint64_t SlotService::calculate_total_dps(const std::vector<PickaxeSlot>& slots) const {
    uint64_t total = 0;
    for (const auto& slot : slots) {
        total += slot.dps;
    }
    return total;
}
