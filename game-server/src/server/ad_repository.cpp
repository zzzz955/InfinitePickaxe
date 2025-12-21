#include "ad_repository.h"
#include <pqxx/pqxx>
#include <spdlog/spdlog.h>
#include <ctime>
#include <iomanip>
#include <sstream>

AdCounter AdRepository::get_or_create_ad_counter(const std::string& user_id, const std::string& ad_type) {
    AdCounter counter;
    counter.user_id = user_id;
    counter.ad_type = ad_type;

    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        auto kst_row = tx.exec1("SELECT (CURRENT_TIMESTAMP AT TIME ZONE 'Asia/Seoul')::date AS d");
        auto kst_date_str = kst_row["d"].as<std::string>();
        std::tm tm_kst = {};
        std::istringstream ss_kst(kst_date_str);
        ss_kst >> std::get_time(&tm_kst, "%Y-%m-%d");
        auto kst_date = std::chrono::system_clock::from_time_t(std::mktime(&tm_kst));

        auto existing = tx.exec_params(
            "SELECT ad_count, reset_date "
            "FROM game_schema.user_ad_counters "
            "WHERE user_id = $1 AND ad_type = $2",
            user_id, ad_type
        );

        if (existing.empty()) {
            tx.exec_params(
                "INSERT INTO game_schema.user_ad_counters (user_id, ad_type, ad_count, reset_date) "
                "VALUES ($1, $2, 0, $3)",
                user_id, ad_type, kst_date_str
            );
            counter.ad_count = 0;
            counter.reset_date = kst_date;
        } else {
            auto row = existing[0];
            auto date_str = row["reset_date"].as<std::string>();
            if (date_str < kst_date_str) {
                tx.exec_params(
                    "UPDATE game_schema.user_ad_counters "
                    "SET ad_count = 0, reset_date = $3 "
                    "WHERE user_id = $1 AND ad_type = $2",
                    user_id, ad_type, kst_date_str
                );
                counter.ad_count = 0;
                counter.reset_date = kst_date;
            } else {
                counter.ad_count = row["ad_count"].as<uint32_t>();
                std::tm tm = {};
                std::istringstream ss(date_str);
                ss >> std::get_time(&tm, "%Y-%m-%d");
                counter.reset_date = std::chrono::system_clock::from_time_t(std::mktime(&tm));
            }
        }

        tx.commit();
    } catch (const std::exception& ex) {
        spdlog::error("get_or_create_ad_counter failed: user={} ad_type={} error={}",
                      user_id, ad_type, ex.what());
        counter.ad_count = 0;
        counter.reset_date = std::chrono::system_clock::now();
    }

    return counter;
}

bool AdRepository::increment_ad_counter(const std::string& user_id, const std::string& ad_type) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        tx.exec_params(
            "WITH kst_today AS (SELECT (CURRENT_TIMESTAMP AT TIME ZONE 'Asia/Seoul')::date AS d) "
            "INSERT INTO game_schema.user_ad_counters (user_id, ad_type, ad_count, reset_date) "
            "SELECT $1, $2, 1, d FROM kst_today "
            "ON CONFLICT (user_id, ad_type) DO UPDATE "
            "SET ad_count = CASE WHEN user_ad_counters.reset_date < (SELECT d FROM kst_today) THEN 1 "
            "                    ELSE user_ad_counters.ad_count + 1 END, "
            "    reset_date = (SELECT d FROM kst_today)",
            user_id, ad_type
        );

        tx.commit();
        spdlog::debug("increment_ad_counter: user={} ad_type={}", user_id, ad_type);
        return true;
    } catch (const std::exception& ex) {
        spdlog::error("increment_ad_counter failed: user={} ad_type={} error={}",
                      user_id, ad_type, ex.what());
        return false;
    }
}

std::vector<AdCounter> AdRepository::get_all_ad_counters(const std::string& user_id) {
    std::vector<AdCounter> counters;

    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        tx.exec_params(
            "WITH kst_today AS (SELECT (CURRENT_TIMESTAMP AT TIME ZONE 'Asia/Seoul')::date AS d) "
            "UPDATE game_schema.user_ad_counters "
            "SET ad_count = 0, reset_date = (SELECT d FROM kst_today) "
            "WHERE user_id = $1 AND reset_date < (SELECT d FROM kst_today)",
            user_id
        );

        auto res = tx.exec_params(
            "SELECT user_id, ad_type, ad_count, reset_date "
            "FROM game_schema.user_ad_counters "
            "WHERE user_id = $1",
            user_id
        );

        for (auto row : res) {
            AdCounter counter;
            counter.user_id = row["user_id"].as<std::string>();
            counter.ad_type = row["ad_type"].as<std::string>();
            counter.ad_count = row["ad_count"].as<uint32_t>();

            auto date_str = row["reset_date"].as<std::string>();
            std::tm tm = {};
            std::istringstream ss(date_str);
            ss >> std::get_time(&tm, "%Y-%m-%d");
            counter.reset_date = std::chrono::system_clock::from_time_t(std::mktime(&tm));

            counters.push_back(counter);
        }

        tx.commit();
    } catch (const std::exception& ex) {
        spdlog::error("get_all_ad_counters failed: user={} error={}", user_id, ex.what());
    }

    return counters;
}
