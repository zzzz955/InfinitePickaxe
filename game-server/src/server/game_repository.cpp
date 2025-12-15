#include "game_repository.h"
#include "connection_pool.h"
#include <pqxx/pqxx>
#include <spdlog/spdlog.h>
#include <sstream>

GameRepository::GameRepository(ConnectionPool& pool)
    : pool_(pool) {}

bool GameRepository::ensure_user_initialized(const std::string& user_id) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        tx.exec_params(
            "INSERT INTO game_schema.user_game_data (user_id) VALUES ($1) "
            "ON CONFLICT (user_id) DO NOTHING",
            user_id);
        // 슬롯 0번 생성: 레벨 0, 티어 1, 공격력 10, 공격속도 1.0 (100)
        // 크리티컬 확률 5% (500), 크리티컬 데미지 150% (15000)
        tx.exec_params(
            "INSERT INTO game_schema.pickaxe_slots "
            "(user_id, slot_index, level, tier, attack_power, attack_speed_x100, "
            " critical_hit_percent, critical_damage, dps) "
            "VALUES ($1, 0, 0, 1, 10, 100, 500, 15000, 10) "
            "ON CONFLICT (user_id, slot_index) DO NOTHING",
            user_id);
        tx.commit();
        spdlog::debug("User {} initialized with slot 0 (attack_power=10, crit=5%)", user_id);
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
