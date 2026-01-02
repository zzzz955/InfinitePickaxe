#include "offline_repository.h"
#include <pqxx/pqxx>
#include <spdlog/spdlog.h>
#include <ctime>
#include <iomanip>
#include <sstream>

namespace {
std::chrono::system_clock::time_point parse_date(const std::string& date_str) {
    std::tm tm = {};
    std::istringstream ss(date_str);
    ss >> std::get_time(&tm, "%Y-%m-%d");
    return std::chrono::system_clock::from_time_t(std::mktime(&tm));
}
} // namespace

OfflineState OfflineRepository::get_or_create_state(const std::string& user_id, uint32_t initial_seconds) {
    OfflineState state;
    state.user_id = user_id;
    state.current_offline_seconds = initial_seconds;
    state.offline_date = std::chrono::system_clock::now();

    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        auto row = tx.exec_params1(
            "WITH kst_today AS (SELECT (CURRENT_TIMESTAMP AT TIME ZONE 'Asia/Seoul')::date AS d) "
            "INSERT INTO game_schema.user_offline_state (user_id, offline_date, current_offline_hours) "
            "VALUES ($1, (SELECT d FROM kst_today), $2) "
            "ON CONFLICT (user_id) DO UPDATE "
            "SET current_offline_hours = CASE WHEN user_offline_state.offline_date < (SELECT d FROM kst_today) THEN $2 "
            "                                    ELSE user_offline_state.current_offline_hours END, "
            "    offline_date = CASE WHEN user_offline_state.offline_date < (SELECT d FROM kst_today) THEN (SELECT d FROM kst_today) "
            "                         ELSE user_offline_state.offline_date END "
            "RETURNING offline_date, current_offline_hours",
            user_id, static_cast<int64_t>(initial_seconds));

        state.current_offline_seconds = row["current_offline_hours"].as<uint32_t>();
        state.offline_date = parse_date(row["offline_date"].as<std::string>());
        tx.commit();
    } catch (const std::exception& ex) {
        spdlog::error("get_or_create_state failed: user={} error={}", user_id, ex.what());
    }

    return state;
}

std::optional<uint32_t> OfflineRepository::add_offline_seconds(const std::string& user_id, uint32_t delta_seconds, uint32_t initial_seconds) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        auto row = tx.exec_params1(
            "WITH kst_today AS (SELECT (CURRENT_TIMESTAMP AT TIME ZONE 'Asia/Seoul')::date AS d) "
            "INSERT INTO game_schema.user_offline_state (user_id, offline_date, current_offline_hours) "
            "VALUES ($1, (SELECT d FROM kst_today), $3::integer + $2::integer) "
            "ON CONFLICT (user_id) DO UPDATE "
            "SET current_offline_hours = (CASE WHEN user_offline_state.offline_date < (SELECT d FROM kst_today) THEN $3::integer "
            "                                   ELSE user_offline_state.current_offline_hours END) + $2::integer, "
            "    offline_date = CASE WHEN user_offline_state.offline_date < (SELECT d FROM kst_today) THEN (SELECT d FROM kst_today) "
            "                         ELSE user_offline_state.offline_date END "
            "RETURNING current_offline_hours",
            user_id,
            static_cast<int64_t>(delta_seconds),
            static_cast<int64_t>(initial_seconds));

        uint32_t total = row["current_offline_hours"].as<uint32_t>();
        tx.commit();
        return total;
    } catch (const std::exception& ex) {
        spdlog::error("add_offline_seconds failed: user={} error={}", user_id, ex.what());
        return std::nullopt;
    }
}

std::optional<uint32_t> OfflineRepository::set_offline_seconds_today(const std::string& user_id, uint32_t seconds) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        auto row = tx.exec_params1(
            "WITH kst_today AS (SELECT (CURRENT_TIMESTAMP AT TIME ZONE 'Asia/Seoul')::date AS d) "
            "INSERT INTO game_schema.user_offline_state (user_id, offline_date, current_offline_hours) "
            "VALUES ($1, (SELECT d FROM kst_today), $2::integer) "
            "ON CONFLICT (user_id) DO UPDATE "
            "SET current_offline_hours = $2::integer, "
            "    offline_date = (SELECT d FROM kst_today) "
            "RETURNING current_offline_hours",
            user_id,
            static_cast<int64_t>(seconds));

        uint32_t total = row["current_offline_hours"].as<uint32_t>();
        tx.commit();
        return total;
    } catch (const std::exception& ex) {
        spdlog::error("set_offline_seconds_today failed: user={} error={}", user_id, ex.what());
        return std::nullopt;
    }
}
