#include "achievement_repository.h"
#include <pqxx/pqxx>
#include <spdlog/spdlog.h>

std::optional<uint64_t> AchievementRepository::add_progress(const std::string& user_id,
                                                            const std::string& achievement_type,
                                                            uint64_t delta) {
    if (delta == 0 || achievement_type.empty()) {
        return std::nullopt;
    }

    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        auto row = tx.exec_params1(
            "INSERT INTO game_schema.user_achievement_counters (user_id, achievement_type, current_value) "
            "VALUES ($1, $2, $3) "
            "ON CONFLICT (user_id, achievement_type) DO UPDATE "
            "SET current_value = user_achievement_counters.current_value + $3 "
            "RETURNING current_value",
            user_id, achievement_type, static_cast<int64_t>(delta));
        uint64_t value = row[0].as<uint64_t>();
        tx.commit();
        return value;
    } catch (const std::exception& ex) {
        spdlog::error("add_progress failed: user={} type={} error={}", user_id, achievement_type, ex.what());
        return std::nullopt;
    }
}

std::optional<uint64_t> AchievementRepository::get_progress(const std::string& user_id,
                                                            const std::string& achievement_type) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        auto res = tx.exec_params(
            "SELECT current_value FROM game_schema.user_achievement_counters "
            "WHERE user_id = $1 AND achievement_type = $2",
            user_id, achievement_type);
        if (res.empty()) {
            return std::nullopt;
        }
        uint64_t value = res[0]["current_value"].as<uint64_t>();
        tx.commit();
        return value;
    } catch (const std::exception& ex) {
        spdlog::error("get_progress failed: user={} type={} error={}", user_id, achievement_type, ex.what());
        return std::nullopt;
    }
}

std::optional<uint32_t> AchievementRepository::get_last_claimed_step(const std::string& user_id,
                                                                     uint32_t chain_id) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        auto res = tx.exec_params(
            "SELECT last_claimed_step FROM game_schema.user_achievement_chains "
            "WHERE user_id = $1 AND chain_id = $2",
            user_id, static_cast<int32_t>(chain_id));
        if (res.empty()) {
            return std::nullopt;
        }
        uint32_t value = res[0]["last_claimed_step"].as<uint32_t>();
        tx.commit();
        return value;
    } catch (const std::exception& ex) {
        spdlog::error("get_last_claimed_step failed: user={} chain_id={} error={}", user_id, chain_id, ex.what());
        return std::nullopt;
    }
}

bool AchievementRepository::try_claim_chain_step(const std::string& user_id,
                                                 uint32_t chain_id,
                                                 uint32_t expected_prev_step,
                                                 uint32_t new_step) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        auto res = tx.exec_params(
            "WITH upsert AS ( "
            "  INSERT INTO game_schema.user_achievement_chains (user_id, chain_id, last_claimed_step) "
            "  SELECT $1, $2, $3 WHERE $4 = 0 "
            "  ON CONFLICT (user_id, chain_id) DO UPDATE "
            "  SET last_claimed_step = $3 "
            "  WHERE user_achievement_chains.last_claimed_step = $4 "
            "  RETURNING last_claimed_step "
            ") "
            "SELECT last_claimed_step FROM upsert",
            user_id, static_cast<int32_t>(chain_id), static_cast<int32_t>(new_step),
            static_cast<int32_t>(expected_prev_step));
        tx.commit();
        return !res.empty();
    } catch (const std::exception& ex) {
        spdlog::error("try_claim_chain_step failed: user={} chain_id={} error={}", user_id, chain_id, ex.what());
        return false;
    }
}

std::vector<AchievementCounter> AchievementRepository::get_all_counters(const std::string& user_id) {
    std::vector<AchievementCounter> counters;
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        auto res = tx.exec_params(
            "SELECT user_id, achievement_type, current_value "
            "FROM game_schema.user_achievement_counters "
            "WHERE user_id = $1",
            user_id);
        for (auto row : res) {
            AchievementCounter counter;
            counter.user_id = row["user_id"].as<std::string>();
            counter.achievement_type = row["achievement_type"].as<std::string>();
            counter.current_value = row["current_value"].as<uint64_t>();
            counters.push_back(counter);
        }
        tx.commit();
    } catch (const std::exception& ex) {
        spdlog::error("get_all_counters failed: user={} error={}", user_id, ex.what());
    }
    return counters;
}

std::vector<AchievementChain> AchievementRepository::get_all_chains(const std::string& user_id) {
    std::vector<AchievementChain> chains;
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        auto res = tx.exec_params(
            "SELECT user_id, chain_id, last_claimed_step "
            "FROM game_schema.user_achievement_chains "
            "WHERE user_id = $1",
            user_id);
        for (auto row : res) {
            AchievementChain chain;
            chain.user_id = row["user_id"].as<std::string>();
            chain.chain_id = row["chain_id"].as<uint32_t>();
            chain.last_claimed_step = row["last_claimed_step"].as<uint32_t>();
            chains.push_back(chain);
        }
        tx.commit();
    } catch (const std::exception& ex) {
        spdlog::error("get_all_chains failed: user={} error={}", user_id, ex.what());
    }
    return chains;
}
