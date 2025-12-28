#include "game_repository.h"
#include "connection_pool.h"
#include "metadata/metadata_loader.h"
#include <pqxx/pqxx>
#include <spdlog/spdlog.h>
#include <sstream>
#include <cmath>

GameRepository::GameRepository(ConnectionPool& pool, const MetadataLoader& meta)
    : pool_(pool), meta_(meta) {}

bool GameRepository::ensure_user_initialized(const std::string& user_id) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        const auto& defaults = meta_.new_user_defaults();

        bool unlocked_flags[4] = {false, false, false, false};
        std::vector<uint32_t> slot_indices;
        for (uint32_t idx : defaults.initial_unlocked_pickaxe_slots) {
            if (idx < 4 && !unlocked_flags[idx]) {
                unlocked_flags[idx] = true;
                slot_indices.push_back(idx);
            }
        }
        if (slot_indices.empty()) {
            unlocked_flags[0] = true;
            slot_indices.push_back(0);
        }

        bool gem_slot_flags[6] = {false, false, false, false, false, false};
        std::vector<uint32_t> gem_slot_indices;
        for (uint32_t idx : defaults.initial_unlocked_gem_slots) {
            if (idx < 6 && !gem_slot_flags[idx]) {
                gem_slot_flags[idx] = true;
                gem_slot_indices.push_back(idx);
            }
        }
        if (gem_slot_indices.empty()) {
            gem_slot_indices.push_back(0);
        }

        tx.exec_params(
            "INSERT INTO game_schema.user_game_data (user_id, gold, crystal, unlocked_slots) "
            "VALUES ($1, $2, $3, ARRAY[$4::boolean, $5::boolean, $6::boolean, $7::boolean]) "
            "ON CONFLICT (user_id) DO NOTHING",
            user_id, static_cast<int64_t>(defaults.initial_gold), static_cast<int32_t>(defaults.initial_crystal),
            unlocked_flags[0], unlocked_flags[1], unlocked_flags[2], unlocked_flags[3]);

        uint32_t level = defaults.initial_pickaxe_level;
        uint32_t tier = 1;
        uint64_t attack_power = 10;
        uint32_t attack_speed = 10000;
        uint64_t dps = 10;

        const uint32_t crit_percent = defaults.initial_critical_hit_percent;
        const uint32_t crit_damage = defaults.initial_critical_damage;
        const uint32_t pity_bonus = defaults.initial_pity_bonus;

        const PickaxeLevel* pl = meta_.pickaxe_level(level);
        if (!pl && level != 0) {
            spdlog::warn("pickaxe_level({}) missing in metadata, fallback to level 0", level);
            pl = meta_.pickaxe_level(0);
        }
        if (pl) {
            level = pl->level;
            tier = pl->tier;
            attack_power = pl->attack_power;
            attack_speed = static_cast<uint32_t>(std::llround(pl->attack_speed));
            if (attack_speed == 0) {
                attack_speed = 10000;
            }
            double attack_speed_value = static_cast<double>(attack_speed) / 10000.0;
            double crit_rate = static_cast<double>(crit_percent) / 10000.0;
            double crit_mult = static_cast<double>(crit_damage) / 10000.0;
            double expected_dps = static_cast<double>(attack_power) * attack_speed_value *
                                  (1.0 + crit_rate * (crit_mult - 1.0));
            dps = static_cast<uint64_t>(std::llround(expected_dps));
            if (dps == 0) {
                dps = pl->dps;
            }
        } else {
            spdlog::warn("pickaxe_level(0) missing in metadata, using defaults");
        }

        std::vector<std::string> inserted_slot_ids;
        for (uint32_t slot_index : slot_indices) {
            auto slot_insert = tx.exec_params(
                "INSERT INTO game_schema.pickaxe_slots "
                "(user_id, slot_index, level, tier, attack_power, attack_speed, "
                " critical_hit_percent, critical_damage, dps, pity_bonus) "
                "VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10) "
                "ON CONFLICT (user_id, slot_index) DO NOTHING RETURNING slot_id",
                user_id, static_cast<int32_t>(slot_index), static_cast<int32_t>(level), static_cast<int32_t>(tier),
                static_cast<int64_t>(attack_power), static_cast<int32_t>(attack_speed),
                static_cast<int32_t>(crit_percent), static_cast<int32_t>(crit_damage),
                static_cast<int64_t>(dps), static_cast<int32_t>(pity_bonus));

            if (!slot_insert.empty()) {
                inserted_slot_ids.push_back(slot_insert[0][0].as<std::string>());
            }
        }

        if (!inserted_slot_ids.empty()) {
            auto total_row = tx.exec_params1(
                "SELECT COALESCE(SUM(dps), 0) FROM game_schema.pickaxe_slots WHERE user_id = $1",
                user_id);
            uint64_t total_dps = total_row[0].as<int64_t>();

            tx.exec_params(
                "UPDATE game_schema.user_game_data "
                "SET total_dps = $2, highest_pickaxe_level = GREATEST(highest_pickaxe_level, $3) "
                "WHERE user_id = $1",
                user_id, static_cast<int64_t>(total_dps), static_cast<int32_t>(level));

            uint32_t base_capacity = meta_.gem_inventory_config().base_capacity;
            tx.exec_params(
                "INSERT INTO game_schema.user_gem_inventory (user_id, current_capacity) "
                "VALUES ($1, $2) ON CONFLICT (user_id) DO NOTHING",
                user_id, static_cast<int32_t>(base_capacity));

            for (const auto& pickaxe_slot_id : inserted_slot_ids) {
                for (uint32_t gem_slot_index : gem_slot_indices) {
                    tx.exec_params(
                        "INSERT INTO game_schema.pickaxe_gem_slots "
                        "(pickaxe_slot_id, gem_slot_index, is_unlocked, unlocked_at) "
                        "VALUES ($1::uuid, $2, TRUE, NOW()) "
                        "ON CONFLICT (pickaxe_slot_id, gem_slot_index) DO NOTHING",
                        pickaxe_slot_id, static_cast<int32_t>(gem_slot_index));
                }
            }
        }
        tx.commit();
        spdlog::debug("User {} initialized with {} slot(s) (level={}, tier={}, ap={}, as={}, dps={})",
                      user_id, slot_indices.size(), level, tier, attack_power, attack_speed, dps);
        return true;
    } catch (const std::exception& ex) {
        spdlog::error("DB init failed for user {}: {}", user_id, ex.what());
        return false;
    }
}

UserGameData GameRepository::get_user_game_data(const std::string& user_id) {
    UserGameData data{};
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        auto row = tx.exec_params1(
            "SELECT gold, crystal, unlocked_slots, total_dps, "
            "       current_mineral_id, current_mineral_hp "
            "FROM game_schema.user_game_data WHERE user_id = $1",
            user_id);

        data.gold = row[0].as<uint64_t>();
        data.crystal = row[1].as<uint32_t>();

        // PostgreSQL BOOLEAN[] 배열 파싱
        auto slots_array = row[2].as<std::string>();
        // 형식: {t,f,f,f} 또는 {true,false,false,false}
        data.unlocked_slots.clear();
        for (char c : slots_array) {
            if (c == 't' || c == 'T') {
                data.unlocked_slots.push_back(true);
            } else if (c == 'f' || c == 'F') {
                data.unlocked_slots.push_back(false);
            }
        }
        // 항상 4개 슬롯 보장
        while (data.unlocked_slots.size() < 4) {
            data.unlocked_slots.push_back(false);
        }

        data.total_dps = row[3].as<uint64_t>();

        // current_mineral_id, current_mineral_hp는 nullable
        if (!row[4].is_null()) {
            data.current_mineral_id = row[4].as<uint32_t>();
        }
        if (!row[5].is_null()) {
            data.current_mineral_hp = row[5].as<uint64_t>();
        }

        tx.commit();
    } catch (const std::exception& ex) {
        spdlog::error("Failed to get user game data for {}: {}", user_id, ex.what());
        // 기본값 반환
        data.gold = 0;
        data.crystal = 0;
        data.unlocked_slots = {true, false, false, false};
        data.total_dps = 10;  // 초기 DPS
        data.current_mineral_id = std::nullopt;
        data.current_mineral_hp = std::nullopt;
    }
    return data;
}

std::optional<uint32_t> GameRepository::add_crystal(const std::string& user_id, uint32_t delta) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        auto row = tx.exec_params1(
            "UPDATE game_schema.user_game_data "
            "SET crystal = crystal + $2 "
            "WHERE user_id = $1 "
            "RETURNING crystal",
            user_id, static_cast<int64_t>(delta));
        uint32_t total = row[0].as<uint32_t>();
        tx.commit();
        return total;
    } catch (const std::exception& ex) {
        spdlog::error("add_crystal failed for user {}: {}", user_id, ex.what());
        return std::nullopt;
    }
}

bool GameRepository::set_current_mineral(const std::string& user_id, uint32_t mineral_id, uint64_t mineral_hp) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        tx.exec_params(
            "UPDATE game_schema.user_game_data "
            "SET current_mineral_id = $2, current_mineral_hp = $3 "
            "WHERE user_id = $1",
            user_id, static_cast<int32_t>(mineral_id), static_cast<int64_t>(mineral_hp));
        tx.commit();
        spdlog::debug("set_current_mineral: user={} mineral_id={} hp={}", user_id, mineral_id, mineral_hp);
        return true;
    } catch (const std::exception& ex) {
        spdlog::error("set_current_mineral failed for user {}: {}", user_id, ex.what());
        return false;
    }
}

GemInventoryInfo GameRepository::get_gem_inventory_info(const std::string& user_id) {
    GemInventoryInfo info{};
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        // 인벤토리 용량 조회
        auto inv_row = tx.exec_params1(
            "SELECT current_capacity FROM game_schema.user_gem_inventory WHERE user_id = $1",
            user_id);
        info.capacity = inv_row[0].as<uint32_t>();

        // 보유 보석 개수 조회
        auto count_row = tx.exec_params1(
            "SELECT COUNT(*) FROM game_schema.user_gems WHERE user_id = $1",
            user_id);
        info.total_gems = count_row[0].as<uint32_t>();

        tx.commit();
    } catch (const std::exception& ex) {
        spdlog::error("get_gem_inventory_info failed for user {}: {}", user_id, ex.what());
        // 기본값 반환 (메타데이터 기반)
        info.capacity = meta_.gem_inventory_config().base_capacity;
        info.total_gems = 0;
    }
    return info;
}
