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
            "       attack_power, attack_speed_x100, dps, pity_bonus "
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
            "       attack_power, attack_speed_x100, dps, pity_bonus "
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
                                   uint64_t attack_power, uint32_t attack_speed_x100, uint64_t dps) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        tx.exec_params(
            "INSERT INTO game_schema.pickaxe_slots "
            "(user_id, slot_index, level, tier, attack_power, attack_speed_x100, dps, pity_bonus) "
            "VALUES ($1, $2, $3, $4, $5, $6, $7, 0) "
            "ON CONFLICT (user_id, slot_index) DO NOTHING",
            user_id, slot_index, level, tier, attack_power, attack_speed_x100, dps
        );
        tx.commit();
        spdlog::debug("create_slot: user={} slot={} level={} attack_power={} attack_speed={}",
                      user_id, slot_index, level, attack_power, attack_speed_x100);
        return true;
    } catch (const std::exception& ex) {
        spdlog::error("create_slot failed for user {} slot {}: {}", user_id, slot_index, ex.what());
        return false;
    }
}

bool SlotRepository::update_slot(const std::string& user_id, uint32_t slot_index,
                                   uint32_t new_level, uint32_t new_tier,
                                   uint64_t new_attack_power, uint32_t new_attack_speed_x100,
                                   uint64_t new_dps, uint32_t new_pity_bonus) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        tx.exec_params(
            "UPDATE game_schema.pickaxe_slots "
            "SET level = $3, tier = $4, attack_power = $5, attack_speed_x100 = $6, "
            "    dps = $7, pity_bonus = $8, updated_at = NOW(), last_upgraded_at = NOW() "
            "WHERE user_id = $1 AND slot_index = $2",
            user_id, slot_index, new_level, new_tier,
            new_attack_power, new_attack_speed_x100, new_dps, new_pity_bonus
        );
        tx.commit();
        spdlog::debug("update_slot: user={} slot={} level={} attack_power={} attack_speed={} dps={}",
                      user_id, slot_index, new_level, new_attack_power, new_attack_speed_x100, new_dps);
        return true;
    } catch (const std::exception& ex) {
        spdlog::error("update_slot failed for user {} slot {}: {}", user_id, slot_index, ex.what());
        return false;
    }
}
