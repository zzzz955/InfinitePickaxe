#include "upgrade_repository.h"
#include <pqxx/pqxx>
#include <spdlog/spdlog.h>
#include <random>
#include <cmath>

namespace {
double clamp(double v, double lo, double hi) {
    if (v < lo) return lo;
    if (v > hi) return hi;
    return v;
}
}

UpgradeRepository::UpgradeAttemptResult UpgradeRepository::try_upgrade_with_probability(
    const std::string& user_id,
    uint32_t slot_index,
    uint32_t target_level,
    uint64_t target_dps,
    uint64_t cost,
    const UpgradeRules& rules) {
    UpgradeAttemptResult res;
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        // 슬롯 잠금 + 현재 상태 조회
        auto slot_row = tx.exec_params(
            "SELECT level, tier, pity_bonus, dps "
            "FROM game_schema.pickaxe_slots "
            "WHERE user_id = $1 AND slot_index = $2 FOR UPDATE",
            user_id, slot_index);
        if (slot_row.empty()) {
            res.invalid_slot = true;
            tx.abort();
            return res;
        }

        uint32_t current_level = slot_row[0][0].as<uint32_t>();
        uint32_t tier = slot_row[0][1].as<uint32_t>();
        uint32_t current_pity = slot_row[0][2].as<uint32_t>();
        uint64_t current_dps = slot_row[0][3].as<int64_t>();

        res.final_level = current_level;
        res.final_dps = current_dps;
        res.tier = tier;

        if (target_level != current_level + 1) {
            res.invalid_target = true;
            tx.abort();
            return res;
        }

        auto gold_row = tx.exec_params(
            "SELECT gold FROM game_schema.user_game_data WHERE user_id = $1 FOR UPDATE",
            user_id);
        if (gold_row.empty()) {
            res.invalid_slot = true;
            tx.abort();
            return res;
        }
        uint64_t gold = gold_row[0][0].as<int64_t>();
        if (gold < cost) {
            res.insufficient_gold = true;
            tx.abort();
            return res;
        }

        // 확률 계산
        double base_rate = clamp(std::max(rules.base_rate(tier), rules.min_rate), 0.0, 1.0);
        double pity_rate = static_cast<double>(current_pity) / 10000.0;
        double final_rate = clamp(base_rate + pity_rate, 0.0, 1.0);
        double bonus_rate = rules.bonus_rate;

        static thread_local std::mt19937 rng(std::random_device{}());
        std::uniform_real_distribution<double> dist(0.0, 1.0);
        bool success = dist(rng) < final_rate;

        // 골드 차감
        auto gold_update = tx.exec_params(
            "UPDATE game_schema.user_game_data "
            "SET gold = gold - $2, updated_at = NOW() "
            "WHERE user_id = $1 RETURNING gold",
            user_id, static_cast<int64_t>(cost));
        if (gold_update.empty()) {
            res.insufficient_gold = true;
            tx.abort();
            return res;
        }
        res.remaining_gold = gold_update[0][0].as<int64_t>();

        uint32_t new_pity = 0;
        if (success) {
            res.final_level = target_level;
            res.final_dps = target_dps;
            new_pity = 0;
            tx.exec_params(
                "UPDATE game_schema.pickaxe_slots "
                "SET level = $3, dps = $4, pity_bonus = $5, "
                "    updated_at = NOW(), last_upgraded_at = NOW() "
                "WHERE user_id = $1 AND slot_index = $2",
                user_id, slot_index, target_level, static_cast<int64_t>(target_dps), new_pity);
            tx.exec_params(
                "UPDATE game_schema.user_game_data "
                "SET highest_pickaxe_level = GREATEST(highest_pickaxe_level, $2) "
                "WHERE user_id = $1",
                user_id, target_level);
        } else {
            // 실패 시 기본확률 * bonus_rate 만큼 누적, 상한 10000
            uint32_t increment = static_cast<uint32_t>(std::lround(base_rate * bonus_rate * 10000.0));
            new_pity = std::min<uint32_t>(10000, current_pity + increment);
            tx.exec_params(
                "UPDATE game_schema.pickaxe_slots "
                "SET pity_bonus = $3, updated_at = NOW(), last_upgraded_at = NOW() "
                "WHERE user_id = $1 AND slot_index = $2",
                user_id, slot_index, new_pity);
        }

        tx.commit();

        res.success = success;
        res.pity_bonus = new_pity;
        res.base_rate = base_rate;
        res.bonus_rate = bonus_rate;
        res.final_rate = final_rate;
    } catch (const std::exception& ex) {
        spdlog::error("try_upgrade_with_probability failed for user {} slot {}: {}", user_id, slot_index, ex.what());
    }
    return res;
}
