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
        tx.exec_params(
            "INSERT INTO game_schema.user_game_data (user_id) VALUES ($1) "
            "ON CONFLICT (user_id) DO NOTHING",
            user_id);

        // 메타데이터 기반 초기 곡괭이 설정 (레벨 0만 슬롯 0에 배정)
        uint32_t level = 0;
        uint32_t tier = 1;
        uint64_t attack_power = 10;
        uint32_t attack_speed_x100 = 100;
        constexpr uint32_t kCritPercent = 500;   // 5%
        constexpr uint32_t kCritDamage = 15000;  // 150%
        uint64_t dps = 10;

        if (const auto* pl = meta_.pickaxe_level(0)) {
            level = pl->level;
            tier = pl->tier;
            attack_power = pl->attack_power;
            attack_speed_x100 = static_cast<uint32_t>(std::lround(pl->attack_speed * 100.0));
            if (attack_speed_x100 == 0) {
                attack_speed_x100 = 1;
            }
            double attack_speed = static_cast<double>(attack_speed_x100) / 100.0;
            double crit_rate = static_cast<double>(kCritPercent) / 10000.0;
            double crit_mult = static_cast<double>(kCritDamage) / 10000.0;
            double expected_dps = static_cast<double>(attack_power) * attack_speed *
                                  (1.0 + crit_rate * (crit_mult - 1.0));
            dps = static_cast<uint64_t>(std::llround(expected_dps));
            if (dps == 0) {
                dps = pl->dps;
            }
        } else {
            spdlog::warn("pickaxe_level(0) missing in metadata, using defaults");
        }

        auto slot_insert = tx.exec_params(
            "INSERT INTO game_schema.pickaxe_slots "
            "(user_id, slot_index, level, tier, attack_power, attack_speed_x100, "
            " critical_hit_percent, critical_damage, dps, pity_bonus) "
            "VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, 0) "
            "ON CONFLICT (user_id, slot_index) DO NOTHING RETURNING slot_id",
            user_id, 0, static_cast<int32_t>(level), static_cast<int32_t>(tier),
            static_cast<int64_t>(attack_power), static_cast<int32_t>(attack_speed_x100),
            static_cast<int32_t>(kCritPercent), static_cast<int32_t>(kCritDamage),
            static_cast<int64_t>(dps));

        const bool inserted_slot = !slot_insert.empty();
        std::string pickaxe_slot_id;
        if (inserted_slot) {
            pickaxe_slot_id = slot_insert[0][0].as<std::string>();
        }

        if (inserted_slot) {
            auto total_row = tx.exec_params1(
                "SELECT COALESCE(SUM(dps), 0) FROM game_schema.pickaxe_slots WHERE user_id = $1",
                user_id);
            uint64_t total_dps = total_row[0].as<int64_t>();

            tx.exec_params(
                "UPDATE game_schema.user_game_data "
                "SET total_dps = $2, highest_pickaxe_level = GREATEST(highest_pickaxe_level, $3) "
                "WHERE user_id = $1",
                user_id, static_cast<int64_t>(total_dps), static_cast<int32_t>(level));

            // 보석 인벤토리 초기화
            uint32_t base_capacity = meta_.gem_inventory_config().base_capacity;
            tx.exec_params(
                "INSERT INTO game_schema.user_gem_inventory (user_id, current_capacity) "
                "VALUES ($1, $2) ON CONFLICT (user_id) DO NOTHING",
                user_id, static_cast<int32_t>(base_capacity));

            // 곡괭이 슬롯 0번의 보석 슬롯 0번만 해금
            if (!pickaxe_slot_id.empty()) {
                tx.exec_params(
                    "INSERT INTO game_schema.pickaxe_gem_slots "
                    "(pickaxe_slot_id, gem_slot_index, is_unlocked, unlocked_at) "
                    "VALUES ($1::uuid, 0, TRUE, NOW()) "
                    "ON CONFLICT (pickaxe_slot_id, gem_slot_index) DO NOTHING",
                    pickaxe_slot_id);
            }
        }
        tx.commit();
        spdlog::debug("User {} initialized with slot 0 (level={}, tier={}, ap={}, as_x100={}, dps={})",
                      user_id, level, tier, attack_power, attack_speed_x100, dps);
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
