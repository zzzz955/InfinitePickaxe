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
    uint32_t target_attack_speed,
    uint64_t target_dps,
    uint64_t cost,
    const UpgradeRules& rules) {
    UpgradeAttemptResult res;
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        // 슬롯 잠금 + 현재 상태 조회
        auto slot_row = tx.exec_params(
            "SELECT slot_id, level, tier, pity_bonus, attack_power, attack_speed, "
            "       critical_hit_percent, critical_damage, dps "
            "FROM game_schema.pickaxe_slots "
            "WHERE user_id = $1 AND slot_index = $2 FOR UPDATE",
            user_id, slot_index);
        if (slot_row.empty()) {
            res.invalid_slot = true;
            tx.abort();
            return res;
        }

        std::string slot_id = slot_row[0][0].as<std::string>();
        uint32_t current_level = slot_row[0][1].as<uint32_t>();
        uint32_t current_tier = slot_row[0][2].as<uint32_t>();
        uint32_t current_pity_bp = slot_row[0][3].as<uint32_t>();
        uint64_t current_attack_power = slot_row[0][4].as<int64_t>();
        uint32_t current_attack_speed = slot_row[0][5].as<uint32_t>();
        uint32_t critical_hit_percent = slot_row[0][6].as<uint32_t>();
        uint32_t critical_damage = slot_row[0][7].as<uint32_t>();
        uint64_t current_dps = slot_row[0][8].as<int64_t>();

        res.final_level = current_level;
        res.final_tier = current_tier;
        res.final_attack_power = current_attack_power;
        res.final_attack_speed = current_attack_speed;
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
            // 보석 보너스 계산
            auto gem_slots = gem_repo_.get_gem_slots_for_pickaxe(slot_id);
            uint32_t attack_speed_bonus = 0;  // basis 10000 퍼센트 보너스
            uint32_t crit_rate_bonus = 0;     // basis 10000
            uint32_t crit_damage_bonus = 0;   // basis 10000

            for (const auto& gem_slot : gem_slots) {
                if (!gem_slot.equipped_gem.has_value()) {
                    continue;
                }
                const auto& gem = gem_slot.equipped_gem.value();
                const auto* gem_def = meta_.gem_definition(gem.gem_id);
                if (!gem_def) {
                    continue;
                }
                const auto* type = meta_.gem_type(gem_def->type_id);
                if (!type) {
                    continue;
                }
                uint32_t multiplier = gem_def->stat_multiplier; // basis 10000

                if (type->type == "ATTACK_SPEED") {
                    attack_speed_bonus += multiplier;
                } else if (type->type == "CRIT_RATE") {
                    crit_rate_bonus += multiplier;
                } else if (type->type == "CRIT_DMG") {
                    crit_damage_bonus += multiplier;
                }
            }

            // 기본 스탯 + 보석 보너스 적용
            // attack_speed: 퍼센트 곱셈 (base * (10000 + bonus) / 10000)
            uint32_t final_attack_speed = static_cast<uint32_t>(
                (static_cast<uint64_t>(target_attack_speed) * (10000 + attack_speed_bonus)) / 10000);
            // 크리티컬 스탯: 기본값(500, 15000) + 보석 보너스
            constexpr uint32_t DEFAULT_CRITICAL_HIT_PERCENT = 500;    // 5% (basis 10000)
            constexpr uint32_t DEFAULT_CRITICAL_DAMAGE = 15000;       // 150% (basis 10000)
            uint32_t final_critical_hit_percent = DEFAULT_CRITICAL_HIT_PERCENT + crit_rate_bonus;
            uint32_t final_critical_damage = DEFAULT_CRITICAL_DAMAGE + crit_damage_bonus;

            // DPS 재계산 (보석 효과 적용된 스탯 사용)
            double attack_speed_value = static_cast<double>(final_attack_speed) / 10000.0;
            double crit_rate = static_cast<double>(final_critical_hit_percent) / 10000.0;
            double crit_damage_multiplier = static_cast<double>(final_critical_damage) / 10000.0;
            double calculated_dps = static_cast<double>(target_attack_power) * attack_speed_value
                                  * (1.0 + crit_rate * (crit_damage_multiplier - 1.0));

            res.final_level = target_level;
            res.final_tier = target_tier;
            res.final_attack_power = target_attack_power;
            res.final_attack_speed = final_attack_speed;
            res.final_critical_hit_percent = final_critical_hit_percent;
            res.final_critical_damage = final_critical_damage;
            res.final_dps = static_cast<uint64_t>(calculated_dps);
            new_pity = 0;

            // 슬롯 업데이트 (보석 효과가 적용된 스탯 저장)
            tx.exec_params(
                "UPDATE game_schema.pickaxe_slots "
                "SET level = $3, tier = $4, attack_power = $5, attack_speed = $6, "
                "    critical_hit_percent = $7, critical_damage = $8, "
                "    dps = $9, pity_bonus = $10, last_upgraded_at = NOW() "
                "WHERE user_id = $1 AND slot_index = $2",
                user_id, slot_index, target_level, target_tier,
                static_cast<int64_t>(target_attack_power), final_attack_speed,
                final_critical_hit_percent, final_critical_damage,
                static_cast<int64_t>(res.final_dps), new_pity);

            spdlog::debug("upgrade success: user={} slot={} lv={} tier={} ap={} as={} (base={} +{}%) crit={}% (base={}% +{}%) critdmg={}% (base={}% +{}%) dps={}",
                         user_id, slot_index, target_level, target_tier,
                         target_attack_power, final_attack_speed,
                         target_attack_speed, attack_speed_bonus,
                         final_critical_hit_percent / 100.0, critical_hit_percent / 100.0, crit_rate_bonus / 100.0,
                         final_critical_damage / 100.0, critical_damage / 100.0, crit_damage_bonus / 100.0,
                         res.final_dps);

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

            // 실패해도 최신 total_dps를 내려주기 위해 조회 (UI 실시간 반영)
            auto total_dps_row = tx.exec_params(
                "SELECT COALESCE(SUM(dps), 0) FROM game_schema.pickaxe_slots WHERE user_id = $1",
                user_id);
            res.final_total_dps = total_dps_row[0][0].as<int64_t>();
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
