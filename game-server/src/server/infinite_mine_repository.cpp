#include "infinite_mine_repository.h"
#include <pqxx/pqxx>
#include <spdlog/spdlog.h>

std::vector<InfiniteMineProgress> InfiniteMineRepository::get_all_progress(const std::string& user_id) {
    std::vector<InfiniteMineProgress> progress;
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        auto res = tx.exec_params(
            "SELECT floor, "
            "       (first_cleared_at AT TIME ZONE 'Asia/Seoul')::date AS first_cleared_date, "
            "       last_auto_claim_date "
            "FROM game_schema.user_infinite_mine_progress "
            "WHERE user_id = $1 "
            "ORDER BY floor",
            user_id);

        for (const auto& row : res) {
            InfiniteMineProgress entry;
            entry.floor = row["floor"].as<uint32_t>();
            entry.first_cleared_date = row["first_cleared_date"].as<std::string>();
            if (row["last_auto_claim_date"].is_null()) {
                entry.last_auto_claim_date.clear();
            } else {
                entry.last_auto_claim_date = row["last_auto_claim_date"].as<std::string>();
            }
            progress.push_back(std::move(entry));
        }

        tx.commit();
    } catch (const std::exception& ex) {
        spdlog::error("get_all_progress failed: user={} error={}", user_id, ex.what());
    }
    return progress;
}

std::optional<InfiniteMineProgress> InfiniteMineRepository::get_progress(const std::string& user_id, uint32_t floor) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        auto res = tx.exec_params(
            "SELECT floor, "
            "       (first_cleared_at AT TIME ZONE 'Asia/Seoul')::date AS first_cleared_date, "
            "       last_auto_claim_date "
            "FROM game_schema.user_infinite_mine_progress "
            "WHERE user_id = $1 AND floor = $2",
            user_id, static_cast<int32_t>(floor));

        if (res.empty()) {
            return std::nullopt;
        }

        const auto& row = res[0];
        InfiniteMineProgress entry;
        entry.floor = row["floor"].as<uint32_t>();
        entry.first_cleared_date = row["first_cleared_date"].as<std::string>();
        if (row["last_auto_claim_date"].is_null()) {
            entry.last_auto_claim_date.clear();
        } else {
            entry.last_auto_claim_date = row["last_auto_claim_date"].as<std::string>();
        }

        tx.commit();
        return entry;
    } catch (const std::exception& ex) {
        spdlog::error("get_progress failed: user={} floor={} error={}", user_id, floor, ex.what());
        return std::nullopt;
    }
}

uint32_t InfiniteMineRepository::get_highest_cleared_floor(const std::string& user_id) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        auto row = tx.exec_params1(
            "SELECT COALESCE(MAX(floor), 0) FROM game_schema.user_infinite_mine_progress "
            "WHERE user_id = $1",
            user_id);
        uint32_t highest = row[0].as<uint32_t>();
        tx.commit();
        return highest;
    } catch (const std::exception& ex) {
        spdlog::error("get_highest_cleared_floor failed: user={} error={}", user_id, ex.what());
        return 0;
    }
}

std::optional<bool> InfiniteMineRepository::insert_first_clear(const std::string& user_id, uint32_t floor) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        auto res = tx.exec_params(
            "INSERT INTO game_schema.user_infinite_mine_progress "
            "(user_id, floor, first_cleared_at, last_auto_claim_date) "
            "VALUES ($1, $2, NOW(), NULL) "
            "ON CONFLICT (user_id, floor) DO NOTHING "
            "RETURNING floor",
            user_id, static_cast<int32_t>(floor));
        tx.commit();
        return !res.empty();
    } catch (const std::exception& ex) {
        spdlog::error("insert_first_clear failed: user={} floor={} error={}", user_id, floor, ex.what());
        return std::nullopt;
    }
}

bool InfiniteMineRepository::update_auto_claim_date(const std::string& user_id, uint32_t floor,
                                                   const std::string& kst_date) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        auto res = tx.exec_params(
            "UPDATE game_schema.user_infinite_mine_progress "
            "SET last_auto_claim_date = $3::date "
            "WHERE user_id = $1 AND floor = $2",
            user_id, static_cast<int32_t>(floor), kst_date);
        tx.commit();
        return res.affected_rows() > 0;
    } catch (const std::exception& ex) {
        spdlog::error("update_auto_claim_date failed: user={} floor={} error={}", user_id, floor, ex.what());
        return false;
    }
}

bool InfiniteMineRepository::update_auto_claim_dates(const std::string& user_id,
                                                     const std::vector<uint32_t>& floors,
                                                     const std::string& kst_date) {
    if (floors.empty()) {
        return true;
    }
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        for (uint32_t floor : floors) {
            tx.exec_params(
                "UPDATE game_schema.user_infinite_mine_progress "
                "SET last_auto_claim_date = $3::date "
                "WHERE user_id = $1 AND floor = $2",
                user_id, static_cast<int32_t>(floor), kst_date);
        }
        tx.commit();
        return true;
    } catch (const std::exception& ex) {
        spdlog::error("update_auto_claim_dates failed: user={} error={}", user_id, ex.what());
        return false;
    }
}
