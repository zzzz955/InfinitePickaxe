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
        tx.exec_params(
            "INSERT INTO game_schema.pickaxe_slots (user_id, slot_index, level, tier, dps) "
            "VALUES ($1, 0, 0, 1, 10) "
            "ON CONFLICT (user_id, slot_index) DO NOTHING",
            user_id);
        tx.commit();
        return true;
    } catch (const std::exception& ex) {
        spdlog::error("DB init failed for user {}: {}", user_id, ex.what());
        return false;
    }
}
