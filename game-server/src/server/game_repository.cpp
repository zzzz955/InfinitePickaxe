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
        // 슬롯 0번 생성: 레벨 0, 공격력 10, 공격속도 1.0 (100)
        tx.exec_params(
            "INSERT INTO game_schema.pickaxe_slots "
            "(user_id, slot_index, level, tier, attack_power, attack_speed_x100, dps) "
            "VALUES ($1, 0, 0, 1, 10, 100, 10) "
            "ON CONFLICT (user_id, slot_index) DO NOTHING",
            user_id);
        tx.commit();
        spdlog::debug("User {} initialized with slot 0 (attack_power=10, attack_speed=1.0)", user_id);
        return true;
    } catch (const std::exception& ex) {
        spdlog::error("DB init failed for user {}: {}", user_id, ex.what());
        return false;
    }
}
