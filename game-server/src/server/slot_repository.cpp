#include "slot_repository.h"
#include <pqxx/pqxx>
#include <spdlog/spdlog.h>

std::vector<PickaxeSlot> SlotRepository::get_user_slots(const std::string& user_id) {
    std::vector<PickaxeSlot> slots;
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        auto res = tx.exec_params(
            "SELECT slot_id, user_id, slot_index, level, tier, "
            "       attack_power, attack_speed_x100, critical_hit_percent, "
            "       critical_damage, dps, pity_bonus "
            "FROM game_schema.pickaxe_slots "
            "WHERE user_id = $1 "
            "ORDER BY slot_index ASC",
            user_id
        );

        for (auto row : res) {
            PickaxeSlot slot;
            slot.slot_id = row["slot_id"].as<std::string>();
            slot.user_id = row["user_id"].as<std::string>();
            slot.slot_index = row["slot_index"].as<uint32_t>();
            slot.level = row["level"].as<uint32_t>();
            slot.tier = row["tier"].as<uint32_t>();
            slot.attack_power = row["attack_power"].as<uint64_t>();
            slot.attack_speed_x100 = row["attack_speed_x100"].as<uint32_t>();
            slot.critical_hit_percent = row["critical_hit_percent"].as<uint32_t>();
            slot.critical_damage = row["critical_damage"].as<uint32_t>();
            slot.dps = row["dps"].as<uint64_t>();
            slot.pity_bonus = row["pity_bonus"].as<uint32_t>();
            slots.push_back(slot);
        }
    } catch (const std::exception& ex) {
        spdlog::error("get_user_slots failed for user {}: {}", user_id, ex.what());
    }
    return slots;
}

std::optional<PickaxeSlot> SlotRepository::get_slot(const std::string& user_id, uint32_t slot_index) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        auto res = tx.exec_params(
            "SELECT slot_id, user_id, slot_index, level, tier, "
            "       attack_power, attack_speed_x100, critical_hit_percent, "
            "       critical_damage, dps, pity_bonus "
            "FROM game_schema.pickaxe_slots "
            "WHERE user_id = $1 AND slot_index = $2",
            user_id, slot_index
        );

        if (res.empty()) {
            return std::nullopt;
        }

        auto row = res[0];
        PickaxeSlot slot;
        slot.slot_id = row["slot_id"].as<std::string>();
        slot.user_id = row["user_id"].as<std::string>();
        slot.slot_index = row["slot_index"].as<uint32_t>();
        slot.level = row["level"].as<uint32_t>();
        slot.tier = row["tier"].as<uint32_t>();
        slot.attack_power = row["attack_power"].as<uint64_t>();
        slot.attack_speed_x100 = row["attack_speed_x100"].as<uint32_t>();
        slot.critical_hit_percent = row["critical_hit_percent"].as<uint32_t>();
        slot.critical_damage = row["critical_damage"].as<uint32_t>();
        slot.dps = row["dps"].as<uint64_t>();
        slot.pity_bonus = row["pity_bonus"].as<uint32_t>();
        return slot;
    } catch (const std::exception& ex) {
        spdlog::error("get_slot failed for user {} slot {}: {}", user_id, slot_index, ex.what());
        return std::nullopt;
    }
}

bool SlotRepository::create_slot(const std::string& user_id, uint32_t slot_index,
                                   uint32_t level, uint32_t tier,
                                   uint64_t attack_power, uint32_t attack_speed_x100,
                                   uint32_t critical_hit_percent, uint32_t critical_damage,
                                   uint64_t dps) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        tx.exec_params(
            "INSERT INTO game_schema.pickaxe_slots "
            "(user_id, slot_index, level, tier, attack_power, attack_speed_x100, "
            " critical_hit_percent, critical_damage, dps, pity_bonus) "
            "VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, 0) "
            "ON CONFLICT (user_id, slot_index) DO NOTHING",
            user_id, slot_index, level, tier, attack_power, attack_speed_x100,
            critical_hit_percent, critical_damage, dps
        );
        tx.commit();
        spdlog::debug("create_slot: user={} slot={} level={} attack_power={} crit={}%",
                      user_id, slot_index, level, attack_power, critical_hit_percent / 100.0);
        return true;
    } catch (const std::exception& ex) {
        spdlog::error("create_slot failed for user {} slot {}: {}", user_id, slot_index, ex.what());
        return false;
    }
}

bool SlotRepository::update_slot(const std::string& user_id, uint32_t slot_index,
                                   uint32_t new_level, uint32_t new_tier,
                                   uint64_t new_attack_power, uint32_t new_attack_speed_x100,
                                   uint32_t new_critical_hit_percent, uint32_t new_critical_damage,
                                   uint64_t new_dps, uint32_t new_pity_bonus) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        tx.exec_params(
            "UPDATE game_schema.pickaxe_slots "
            "SET level = $3, tier = $4, attack_power = $5, attack_speed_x100 = $6, "
            "    critical_hit_percent = $7, critical_damage = $8, "
            "    dps = $9, pity_bonus = $10, last_upgraded_at = NOW() "
            "WHERE user_id = $1 AND slot_index = $2",
            user_id, slot_index, new_level, new_tier,
            new_attack_power, new_attack_speed_x100,
            new_critical_hit_percent, new_critical_damage,
            new_dps, new_pity_bonus
        );
        tx.commit();
        spdlog::debug("update_slot: user={} slot={} level={} tier={} dps={} crit={}%",
                      user_id, slot_index, new_level, new_tier, new_dps, new_critical_hit_percent / 100.0);
        return true;
    } catch (const std::exception& ex) {
        spdlog::error("update_slot failed for user {} slot {}: {}", user_id, slot_index, ex.what());
        return false;
    }
}

SlotUnlockDBResult SlotRepository::create_and_unlock_slot(const PickaxeSlot& slot, uint32_t crystal_cost) {
    SlotUnlockDBResult result{};
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        // user_game_data 잠금 후 해금 여부/크리스탈 확인
        auto user_row = tx.exec_params(
            "SELECT crystal, unlocked_slots[$2] AS unlocked "
            "FROM game_schema.user_game_data "
            "WHERE user_id = $1 FOR UPDATE",
            slot.user_id, static_cast<int32_t>(slot.slot_index + 1));

        if (user_row.empty()) {
            tx.abort();
            return result;
        }

        uint32_t current_crystal = user_row[0]["crystal"].as<uint32_t>();
        bool already_unlocked = user_row[0]["unlocked"].as<bool>();
        if (already_unlocked) {
            result.already_unlocked = true;
            tx.abort();
            return result;
        }
        if (current_crystal < crystal_cost) {
            result.insufficient_crystal = true;
            result.remaining_crystal = current_crystal;
            tx.abort();
            return result;
        }

        // 슬롯 생성
        tx.exec_params(
            "INSERT INTO game_schema.pickaxe_slots "
            "(user_id, slot_index, level, tier, attack_power, attack_speed_x100, "
            " critical_hit_percent, critical_damage, dps, pity_bonus) "
            "VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10) "
            "ON CONFLICT (user_id, slot_index) DO NOTHING",
            slot.user_id, slot.slot_index, slot.level, slot.tier, slot.attack_power,
            slot.attack_speed_x100, slot.critical_hit_percent, slot.critical_damage,
            slot.dps, slot.pity_bonus);

        // 크리스탈 차감 + 해금 플래그 + total_dps 갱신
        auto update_row = tx.exec_params(
            "UPDATE game_schema.user_game_data "
            "SET crystal = crystal - $2, "
            "    unlocked_slots[$3] = true, "
            "    total_dps = (SELECT COALESCE(SUM(dps), 0) FROM game_schema.pickaxe_slots WHERE user_id = $1) "
            "WHERE user_id = $1 "
            "RETURNING crystal, total_dps",
            slot.user_id,
            static_cast<int64_t>(crystal_cost),
            static_cast<int32_t>(slot.slot_index + 1));

        if (update_row.empty()) {
            tx.abort();
            return result;
        }

        result.remaining_crystal = update_row[0]["crystal"].as<uint32_t>();
        result.total_dps = update_row[0]["total_dps"].as<uint64_t>();
        result.success = true;
        tx.commit();
        spdlog::info("create_and_unlock_slot: user={} slot={} cost={} remaining_crystal={}",
                     slot.user_id, slot.slot_index, crystal_cost, result.remaining_crystal);
    } catch (const std::exception& ex) {
        spdlog::error("create_and_unlock_slot failed for user {} slot {}: {}", slot.user_id, slot.slot_index, ex.what());
    }
    return result;
}
