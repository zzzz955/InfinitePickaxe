#include "upgrade_repository.h"
#include <pqxx/pqxx>
#include <spdlog/spdlog.h>
#include <algorithm>
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
    uint32_t target_tier,
    uint64_t target_attack_power,
    uint32_t target_attack_speed_x100,
    uint64_t target_dps,
    uint64_t cost,
    const UpgradeRules& rules) {
    UpgradeAttemptResult res;
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        // 슬롯 잠금 + 현재 상태 조회
        auto slot_row = tx.exec_params(
            "SELECT level, tier, pity_bonus, attack_power, attack_speed_x100, "
            "       critical_hit_percent, critical_damage, dps "
            "FROM game_schema.pickaxe_slots "
            "WHERE user_id = $1 AND slot_index = $2 FOR UPDATE",
            user_id, slot_index);
        if (slot_row.empty()) {
            res.invalid_slot = true;
            tx.abort();
            return res;
        }

        uint32_t current_level = slot_row[0][0].as<uint32_t>();
        uint32_t current_tier = slot_row[0][1].as<uint32_t>();
        uint32_t current_pity_bp = slot_row[0][2].as<uint32_t>();
        uint64_t current_attack_power = slot_row[0][3].as<int64_t>();
        uint32_t current_attack_speed_x100 = slot_row[0][4].as<uint32_t>();
        uint32_t critical_hit_percent = slot_row[0][5].as<uint32_t>();
        uint32_t critical_damage = slot_row[0][6].as<uint32_t>();
        uint64_t current_dps = slot_row[0][7].as<int64_t>();

        res.final_level = current_level;
        res.final_tier = current_tier;
        res.final_attack_power = current_attack_power;
        res.final_attack_speed_x100 = current_attack_speed_x100;
        res.final_critical_hit_percent = critical_hit_percent;
        res.final_critical_damage = critical_damage;
        res.final_dps = current_dps;

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

        // 확률 계산 (메타데이터 티어 기준)
        uint32_t clamped_pity_bp = std::min<uint32_t>(10000, current_pity_bp);
        double base_rate = clamp(std::max(rules.base_rate(target_tier), rules.min_rate), 0.0, 1.0);
        double current_bonus_rate = static_cast<double>(clamped_pity_bp) / 10000.0;
        double attempt_final_rate = clamp(base_rate + current_bonus_rate, 0.0, 1.0);

        static thread_local std::mt19937 rng(std::random_device{}());
        std::uniform_real_distribution<double> dist(0.0, 1.0);
        bool success = dist(rng) < attempt_final_rate;

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
            // DPS 재계산 (크리티컬 포함)
            // expected_dps = attack_power * attack_speed * (1 + crit_rate * (crit_damage_multiplier - 1))
            double attack_speed = static_cast<double>(target_attack_speed_x100) / 100.0;
            double crit_rate = static_cast<double>(critical_hit_percent) / 10000.0;
            double crit_damage_multiplier = static_cast<double>(critical_damage) / 10000.0;
            double calculated_dps = static_cast<double>(target_attack_power) * attack_speed
                                  * (1.0 + crit_rate * (crit_damage_multiplier - 1.0));

            res.final_level = target_level;
            res.final_tier = target_tier;
            res.final_attack_power = target_attack_power;
            res.final_attack_speed_x100 = target_attack_speed_x100;
            res.final_critical_hit_percent = critical_hit_percent;
            res.final_critical_damage = critical_damage;
            res.final_dps = static_cast<uint64_t>(calculated_dps);
            new_pity = 0;

            // 슬롯 업데이트
            tx.exec_params(
                "UPDATE game_schema.pickaxe_slots "
                "SET level = $3, tier = $4, attack_power = $5, attack_speed_x100 = $6, "
                "    dps = $7, pity_bonus = $8, last_upgraded_at = NOW() "
                "WHERE user_id = $1 AND slot_index = $2",
                user_id, slot_index, target_level, target_tier,
                static_cast<int64_t>(target_attack_power), target_attack_speed_x100,
                static_cast<int64_t>(res.final_dps), new_pity);

            // total_dps 재계산 (모든 슬롯의 DPS 합계)
            auto total_dps_row = tx.exec_params(
                "SELECT COALESCE(SUM(dps), 0) FROM game_schema.pickaxe_slots WHERE user_id = $1",
                user_id);
            res.final_total_dps = total_dps_row[0][0].as<int64_t>();

            // user_game_data 업데이트 (highest_pickaxe_level, total_dps)
            tx.exec_params(
                "UPDATE game_schema.user_game_data "
                "SET highest_pickaxe_level = GREATEST(highest_pickaxe_level, $2), "
                "    total_dps = $3 "
                "WHERE user_id = $1",
                user_id, target_level, static_cast<int64_t>(res.final_total_dps));
        } else {
            // 실패 시 기본확률 * bonus_rate 만큼 누적, 상한 10000
            uint32_t increment = static_cast<uint32_t>(std::lround(base_rate * rules.bonus_rate * 10000.0));
            new_pity = std::min<uint32_t>(10000, clamped_pity_bp + increment);
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
        res.bonus_rate = static_cast<double>(new_pity) / 10000.0;
        res.final_rate = clamp(res.base_rate + res.bonus_rate, 0.0, 1.0);
    } catch (const std::exception& ex) {
        spdlog::error("try_upgrade_with_probability failed for user {} slot {}: {}", user_id, slot_index, ex.what());
    }
    return res;
}
