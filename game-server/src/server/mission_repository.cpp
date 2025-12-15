#include "mission_repository.h"
#include <pqxx/pqxx>
#include <spdlog/spdlog.h>
#include <chrono>
#include <iomanip>
#include <sstream>

// === 광고 카운터 관련 ===

AdCounter MissionRepository::get_or_create_ad_counter(const std::string& user_id, const std::string& ad_type) {
    AdCounter counter;
    counter.user_id = user_id;
    counter.ad_type = ad_type;

    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        // INSERT ON CONFLICT DO UPDATE로 원자적으로 처리 (CURRENT_DATE 사용)
        auto res = tx.exec_params(
            "INSERT INTO game_schema.user_ad_counters (user_id, ad_type, ad_count, reset_date) "
            "VALUES ($1, $2, 0, CURRENT_DATE) "
            "ON CONFLICT (user_id, ad_type) DO UPDATE "
            "SET ad_count = CASE WHEN user_ad_counters.reset_date < CURRENT_DATE THEN 0 "
            "                    ELSE user_ad_counters.ad_count END, "
            "    reset_date = CURRENT_DATE "
            "RETURNING ad_count, reset_date",
            user_id, ad_type
        );

        if (!res.empty()) {
            auto row = res[0];
            counter.ad_count = row["ad_count"].as<uint32_t>();
            // reset_date를 time_point로 변환
            auto date_str = row["reset_date"].as<std::string>();
            std::tm tm = {};
            std::istringstream ss(date_str);
            ss >> std::get_time(&tm, "%Y-%m-%d");
            counter.reset_date = std::chrono::system_clock::from_time_t(std::mktime(&tm));
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

bool MissionRepository::increment_ad_counter(const std::string& user_id, const std::string& ad_type) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        // 오늘 날짜 리셋 후 증가
        tx.exec_params(
            "INSERT INTO game_schema.user_ad_counters (user_id, ad_type, ad_count, reset_date) "
            "VALUES ($1, $2, 1, CURRENT_DATE) "
            "ON CONFLICT (user_id, ad_type) DO UPDATE "
            "SET ad_count = CASE WHEN user_ad_counters.reset_date < CURRENT_DATE THEN 1 "
            "                    ELSE user_ad_counters.ad_count + 1 END, "
            "    reset_date = CURRENT_DATE",
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

std::vector<AdCounter> MissionRepository::get_all_ad_counters(const std::string& user_id) {
    std::vector<AdCounter> counters;

    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

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

            // reset_date를 time_point로 변환
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

// === 일일 미션 정보 관련 ===

DailyMissionInfo MissionRepository::get_or_create_daily_mission_info(const std::string& user_id) {
    DailyMissionInfo info;
    info.user_id = user_id;
    info.mission_date = std::chrono::system_clock::now();
    info.assigned_count = 0;
    info.reroll_count = 0;

    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        // INSERT ON CONFLICT로 오늘 날짜 row 보장
        auto res = tx.exec_params(
            "INSERT INTO game_schema.user_mission_daily (user_id, mission_date, assigned_count, reroll_count) "
            "VALUES ($1, CURRENT_DATE, 0, 0) "
            "ON CONFLICT (user_id, mission_date) DO UPDATE "
            "SET assigned_count = user_mission_daily.assigned_count "
            "RETURNING assigned_count, reroll_count, mission_date",
            user_id
        );

        if (!res.empty()) {
            auto row = res[0];
            info.assigned_count = row["assigned_count"].as<uint32_t>();
            info.reroll_count = row["reroll_count"].as<uint32_t>();

            auto date_str = row["mission_date"].as<std::string>();
            std::tm tm = {};
            std::istringstream ss(date_str);
            ss >> std::get_time(&tm, "%Y-%m-%d");
            info.mission_date = std::chrono::system_clock::from_time_t(std::mktime(&tm));
        }

        tx.commit();
    } catch (const std::exception& ex) {
        spdlog::error("get_or_create_daily_mission_info failed: user={} error={}", user_id, ex.what());
    }

    return info;
}

bool MissionRepository::increment_assigned_count(const std::string& user_id, uint32_t count) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        tx.exec_params(
            "INSERT INTO game_schema.user_mission_daily (user_id, mission_date, assigned_count, reroll_count) "
            "VALUES ($1, CURRENT_DATE, $2, 0) "
            "ON CONFLICT (user_id, mission_date) DO UPDATE "
            "SET assigned_count = user_mission_daily.assigned_count + $2",
            user_id, count
        );

        tx.commit();
        spdlog::debug("increment_assigned_count: user={} count={}", user_id, count);
        return true;
    } catch (const std::exception& ex) {
        spdlog::error("increment_assigned_count failed: user={} error={}", user_id, ex.what());
        return false;
    }
}

bool MissionRepository::increment_reroll_count(const std::string& user_id) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        tx.exec_params(
            "INSERT INTO game_schema.user_mission_daily (user_id, mission_date, assigned_count, reroll_count) "
            "VALUES ($1, CURRENT_DATE, 0, 1) "
            "ON CONFLICT (user_id, mission_date) DO UPDATE "
            "SET reroll_count = user_mission_daily.reroll_count + 1",
            user_id
        );

        tx.commit();
        spdlog::debug("increment_reroll_count: user={}", user_id);
        return true;
    } catch (const std::exception& ex) {
        spdlog::error("increment_reroll_count failed: user={} error={}", user_id, ex.what());
        return false;
    }
}

// === 미션 슬롯 관련 ===

std::optional<MissionSlot> MissionRepository::get_mission_slot(const std::string& user_id, uint32_t slot_no) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        auto res = tx.exec_params(
            "SELECT user_id, slot_no, mission_id, mission_type, target_value, current_value, "
            "       reward_crystal, status, assigned_at, completed_at, claimed_at, expires_at "
            "FROM game_schema.user_mission_slots "
            "WHERE user_id = $1 AND slot_no = $2",
            user_id, slot_no
        );

        if (res.empty()) {
            return std::nullopt;
        }

        auto row = res[0];
        MissionSlot slot;
        slot.user_id = row["user_id"].as<std::string>();
        slot.slot_no = row["slot_no"].as<uint32_t>();
        slot.mission_id = row["mission_id"].as<std::string>();
        slot.mission_type = row["mission_type"].as<std::string>();
        slot.target_value = row["target_value"].as<uint32_t>();
        slot.current_value = row["current_value"].as<uint32_t>();
        slot.reward_crystal = row["reward_crystal"].as<uint32_t>();
        slot.status = row["status"].as<std::string>();

        // timestamp 파싱 (PostgreSQL timestamp → time_point)
        auto parse_timestamp = [](const pqxx::field& f) -> std::chrono::system_clock::time_point {
            auto ts_str = f.as<std::string>();
            std::tm tm = {};
            std::istringstream ss(ts_str);
            ss >> std::get_time(&tm, "%Y-%m-%d %H:%M:%S");
            return std::chrono::system_clock::from_time_t(std::mktime(&tm));
        };

        slot.assigned_at = parse_timestamp(row["assigned_at"]);

        if (!row["completed_at"].is_null()) {
            slot.completed_at = parse_timestamp(row["completed_at"]);
        }
        if (!row["claimed_at"].is_null()) {
            slot.claimed_at = parse_timestamp(row["claimed_at"]);
        }
        if (!row["expires_at"].is_null()) {
            slot.expires_at = parse_timestamp(row["expires_at"]);
        }

        tx.commit();
        return slot;
    } catch (const std::exception& ex) {
        spdlog::error("get_mission_slot failed: user={} slot={} error={}", user_id, slot_no, ex.what());
        return std::nullopt;
    }
}

std::vector<MissionSlot> MissionRepository::get_all_mission_slots(const std::string& user_id) {
    std::vector<MissionSlot> slots;

    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        auto res = tx.exec_params(
            "SELECT user_id, slot_no, mission_id, mission_type, target_value, current_value, "
            "       reward_crystal, status, assigned_at, completed_at, claimed_at, expires_at "
            "FROM game_schema.user_mission_slots "
            "WHERE user_id = $1 "
            "ORDER BY slot_no ASC",
            user_id
        );

        auto parse_timestamp = [](const pqxx::field& f) -> std::chrono::system_clock::time_point {
            auto ts_str = f.as<std::string>();
            std::tm tm = {};
            std::istringstream ss(ts_str);
            ss >> std::get_time(&tm, "%Y-%m-%d %H:%M:%S");
            return std::chrono::system_clock::from_time_t(std::mktime(&tm));
        };

        for (auto row : res) {
            MissionSlot slot;
            slot.user_id = row["user_id"].as<std::string>();
            slot.slot_no = row["slot_no"].as<uint32_t>();
            slot.mission_id = row["mission_id"].as<std::string>();
            slot.mission_type = row["mission_type"].as<std::string>();
            slot.target_value = row["target_value"].as<uint32_t>();
            slot.current_value = row["current_value"].as<uint32_t>();
            slot.reward_crystal = row["reward_crystal"].as<uint32_t>();
            slot.status = row["status"].as<std::string>();

            slot.assigned_at = parse_timestamp(row["assigned_at"]);

            if (!row["completed_at"].is_null()) {
                slot.completed_at = parse_timestamp(row["completed_at"]);
            }
            if (!row["claimed_at"].is_null()) {
                slot.claimed_at = parse_timestamp(row["claimed_at"]);
            }
            if (!row["expires_at"].is_null()) {
                slot.expires_at = parse_timestamp(row["expires_at"]);
            }

            slots.push_back(slot);
        }

        tx.commit();
    } catch (const std::exception& ex) {
        spdlog::error("get_all_mission_slots failed: user={} error={}", user_id, ex.what());
    }

    return slots;
}

bool MissionRepository::assign_mission_to_slot(const std::string& user_id, uint32_t slot_no,
                                               const std::string& mission_type, uint32_t target_value,
                                               uint32_t reward_crystal) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        // gen_random_uuid()로 mission_id 생성
        tx.exec_params(
            "INSERT INTO game_schema.user_mission_slots "
            "(user_id, slot_no, mission_id, mission_type, target_value, current_value, "
            " reward_crystal, status, assigned_at) "
            "VALUES ($1, $2, gen_random_uuid(), $3, $4, 0, $5, 'active', NOW()) "
            "ON CONFLICT (user_id, slot_no) DO UPDATE "
            "SET mission_id = gen_random_uuid(), mission_type = $3, target_value = $4, "
            "    current_value = 0, reward_crystal = $5, status = 'active', "
            "    assigned_at = NOW(), completed_at = NULL, claimed_at = NULL, expires_at = NULL",
            user_id, slot_no, mission_type, target_value, reward_crystal
        );

        tx.commit();
        spdlog::debug("assign_mission_to_slot: user={} slot={} type={} target={}",
                      user_id, slot_no, mission_type, target_value);
        return true;
    } catch (const std::exception& ex) {
        spdlog::error("assign_mission_to_slot failed: user={} slot={} error={}",
                      user_id, slot_no, ex.what());
        return false;
    }
}

bool MissionRepository::update_mission_progress(const std::string& user_id, uint32_t slot_no,
                                                uint32_t new_current_value, const std::string& new_status) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        tx.exec_params(
            "UPDATE game_schema.user_mission_slots "
            "SET current_value = $3, status = $4 "
            "WHERE user_id = $1 AND slot_no = $2",
            user_id, slot_no, new_current_value, new_status
        );

        tx.commit();
        spdlog::debug("update_mission_progress: user={} slot={} value={} status={}",
                      user_id, slot_no, new_current_value, new_status);
        return true;
    } catch (const std::exception& ex) {
        spdlog::error("update_mission_progress failed: user={} slot={} error={}",
                      user_id, slot_no, ex.what());
        return false;
    }
}

bool MissionRepository::complete_mission(const std::string& user_id, uint32_t slot_no) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        tx.exec_params(
            "UPDATE game_schema.user_mission_slots "
            "SET status = 'completed', completed_at = NOW() "
            "WHERE user_id = $1 AND slot_no = $2",
            user_id, slot_no
        );

        tx.commit();
        spdlog::debug("complete_mission: user={} slot={}", user_id, slot_no);
        return true;
    } catch (const std::exception& ex) {
        spdlog::error("complete_mission failed: user={} slot={} error={}", user_id, slot_no, ex.what());
        return false;
    }
}

bool MissionRepository::claim_mission_reward(const std::string& user_id, uint32_t slot_no) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        tx.exec_params(
            "UPDATE game_schema.user_mission_slots "
            "SET status = 'claimed', claimed_at = NOW() "
            "WHERE user_id = $1 AND slot_no = $2",
            user_id, slot_no
        );

        tx.commit();
        spdlog::debug("claim_mission_reward: user={} slot={}", user_id, slot_no);
        return true;
    } catch (const std::exception& ex) {
        spdlog::error("claim_mission_reward failed: user={} slot={} error={}", user_id, slot_no, ex.what());
        return false;
    }
}

bool MissionRepository::delete_mission_slot(const std::string& user_id, uint32_t slot_no) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        tx.exec_params(
            "DELETE FROM game_schema.user_mission_slots "
            "WHERE user_id = $1 AND slot_no = $2",
            user_id, slot_no
        );

        tx.commit();
        spdlog::debug("delete_mission_slot: user={} slot={}", user_id, slot_no);
        return true;
    } catch (const std::exception& ex) {
        spdlog::error("delete_mission_slot failed: user={} slot={} error={}", user_id, slot_no, ex.what());
        return false;
    }
}
