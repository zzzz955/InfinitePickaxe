#include "metadata_loader.h"
#include <fstream>
#include <nlohmann/json.hpp>
#include <sstream>

bool MetadataLoader::load(const std::string& base_path) {
    try {
        // 기존 데이터 클리어
        pickaxe_levels_.clear();
        minerals_.clear();
        missions_.clear();
        milestone_bonuses_.clear();
        ad_types_.clear();
        ad_types_by_id_.clear();
        daily_missions_config_ = DailyMissionConfig{};
        mission_reroll_ = MissionRerollMeta{};
        offline_defaults_ = OfflineDefaults{};
        gem_types_.clear();
        gem_grades_.clear();
        gem_definitions_.clear();
        gem_synthesis_rules_.clear();
        gem_conversion_costs_.clear();
        gem_discard_rewards_.clear();
        gem_slot_unlock_costs_.clear();
        gem_types_by_id_.clear();
        gem_grades_by_id_.clear();
        gem_definitions_by_id_.clear();
        gem_gacha_ = GemGachaMeta{};
        gem_inventory_config_ = GemInventoryConfig{};

        // meta_bundle.json 읽기 (번들 기반 파싱)
        std::ifstream f(base_path + "/meta_bundle.json");
        if (!f.good()) {
            return false;
        }
        nlohmann::json bundle;
        f >> bundle;

        // pickaxe_levels
        {
            nlohmann::json j = bundle["pickaxe_levels"];
            for (auto& e : j) {
                PickaxeLevel pl;
                pl.level = e["level"].get<uint32_t>();
                if (e["tier"].is_string()) {
                    std::string t = e["tier"].get<std::string>();
                    if (!t.empty() && (t[0] == 'T' || t[0] == 't')) {
                        pl.tier = static_cast<uint32_t>(std::stoul(t.substr(1)));
                    } else {
                        pl.tier = e.value("tier_num", 1);
                    }
                } else {
                    pl.tier = e.value("tier", 1);
                }
                pl.attack_power = e["attack_power"].get<uint64_t>();
                pl.attack_speed = e["attack_speed"].get<double>();
                pl.dps = e["dps"].get<uint64_t>();
                pl.cost = e["cost"].get<uint64_t>();
                pickaxe_levels_[pl.level] = pl;
            }
        }

        // minerals
        {
            nlohmann::json j = bundle["minerals"];
            if (j.is_array()) {
                for (auto& e : j) {
                    MineralMeta mm;
                    mm.id = e.value("id", 0);
                    mm.name = e.value("name", "");
                    mm.hp = e.value<uint64_t>("hp", 0);
                    mm.reward = e.value<uint64_t>("reward", e.value<uint64_t>("gold", 0));
                    mm.respawn_time = e.value<uint32_t>("respawn_time", 5);
                    mm.recommended_min_dps = e.value<uint64_t>("recommended_min_DPS", 0);
                    mm.recommended_max_dps = e.value<uint64_t>("recommended_max_DPS", 0);
                    minerals_[mm.id] = mm;
                }
            }
        }

        // daily_missions
        {
            nlohmann::json j = bundle["daily_missions"];
            if (j.is_object()) {
                daily_missions_config_.total_slots = j.value("total_slots", daily_missions_config_.total_slots);
                daily_missions_config_.max_daily_assign = j.value("max_daily_assign", daily_missions_config_.max_daily_assign);
            }
            uint32_t idx = 0;
            auto parse_mission = [&](const nlohmann::json& e) {
                MissionMeta m;
                m.index = idx++;
                m.id = e.value("id", m.index);
                m.type = e.value("type", "");
                m.target = e.value("target", 0);
                m.reward_crystal = e.value("reward_crystal", 0);
                m.description = e.value("description", "");
                m.difficulty = e.value("difficulty", "");
                if (e.contains("mineral_id") && !e["mineral_id"].is_null()) {
                    m.mineral_id = e.value("mineral_id", 0);
                }
                missions_.push_back(m);
            };

            if (j.is_array()) {
                for (auto& e : j) {
                    parse_mission(e);
                }
            } else if (j.contains("missions") && j["missions"].is_array()) {
                for (auto& e : j["missions"]) {
                    parse_mission(e);
                }
            } else if (j.contains("pools")) {
                for (auto& pool : j["pools"].items()) {
                    for (auto& e : pool.value()) {
                        parse_mission(e);
                    }
                }
            }

            if (j.contains("milestone_offline_bonus_hours")) {
                for (auto& e : j["milestone_offline_bonus_hours"]) {
                    MilestoneBonus b;
                    b.completed = e.value("completed", 0);
                    b.bonus_hours = e.value("bonus_hours", 0);
                    if (b.completed > 0 && b.bonus_hours > 0) {
                        milestone_bonuses_.push_back(b);
                    }
                }
            }
        }

        auto to_rate = [](const nlohmann::json& v, double fallback) {
            if (v.is_number_integer()) {
                return static_cast<double>(v.get<int64_t>()) / 10000.0; // basis 10000
            }
            if (v.is_number()) {
                return v.get<double>();
            }
            return fallback;
        };

        // upgrade_rules
        {
            if (bundle.contains("upgrade_rules")) {
                nlohmann::json j = bundle["upgrade_rules"];
                upgrade_rules_.min_rate = to_rate(j["min_rate"], 0.3);
                upgrade_rules_.bonus_rate = to_rate(j["bonus_rate"], 0.1);
                if (j.contains("base_rate_by_tier")) {
                    for (auto& item : j["base_rate_by_tier"].items()) {
                        uint32_t tier = static_cast<uint32_t>(std::stoul(item.key()));
                        double rate = to_rate(item.value(), 1.0);
                        upgrade_rules_.base_rate_by_tier[tier] = rate;
                    }
                }
            } else {
                upgrade_rules_.base_rate_by_tier[1] = 1.0;
                upgrade_rules_.base_rate_by_tier[2] = 0.95;
                upgrade_rules_.base_rate_by_tier[3] = 0.90;
                upgrade_rules_.base_rate_by_tier[4] = 0.85;
            }
        }

        // ads
        {
            if (bundle.contains("ads")) {
                nlohmann::json j = bundle["ads"];
                if (j.contains("ad_types") && j["ad_types"].is_array()) {
                    for (auto& e : j["ad_types"]) {
                        AdTypeMeta ad;
                        ad.id = e.value("id", "");
                        ad.effect = e.value("effect", "");
                        ad.daily_limit = e.value("daily_limit", 0);
                        if (e.contains("rewards_by_view")) {
                            for (auto& r : e["rewards_by_view"]) {
                                ad.rewards_by_view.push_back(r.get<uint32_t>());
                            }
                        }
                        if (e.contains("parameters")) {
                            const auto& p = e["parameters"];
                            if (p.contains("cost_multiplier")) {
                                ad.cost_multiplier = p.value("cost_multiplier", 100);
                            }
                            if (p.contains("apply_to_slots")) {
                                if (p["apply_to_slots"].is_string()) {
                                    std::string v = p["apply_to_slots"].get<std::string>();
                                    ad.apply_to_all_slots = (v == "all");
                                } else {
                                    ad.apply_to_all_slots = p.value("apply_to_slots", true);
                                }
                            }
                            if (p.contains("progress_reset_on_reroll")) {
                                ad.progress_reset_on_reroll = p.value("progress_reset_on_reroll", true);
                            }
                        }
                        if (!ad.id.empty()) {
                            ad_types_.push_back(ad);
                            ad_types_by_id_[ad.id] = ad;
                        }
                    }
                }
            }
        }

        // offline_defaults
        {
            if (bundle.contains("offline_defaults")) {
                nlohmann::json j = bundle["offline_defaults"];
                uint32_t hours = j.value("initial_offline_hours", 0);
                offline_defaults_.initial_offline_seconds = hours * 3600;
            } else {
                offline_defaults_.initial_offline_seconds = 0;
            }
        }

        // mission_reroll
        {
            if (bundle.contains("mission_reroll")) {
                nlohmann::json j = bundle["mission_reroll"];
                mission_reroll_.free_rerolls_per_day = j.value("free_rerolls_per_day", 0);
                mission_reroll_.ad_rerolls_per_day = j.value("ad_rerolls_per_day", 0);
                mission_reroll_.apply_to_all_slots = j.value("apply_to_slots", true);
                mission_reroll_.progress_reset_on_reroll = j.value("progress_reset_on_reroll", true);
            } else {
                mission_reroll_.free_rerolls_per_day = 2;
                mission_reroll_.ad_rerolls_per_day = 3;
                mission_reroll_.apply_to_all_slots = true;
                mission_reroll_.progress_reset_on_reroll = true;
            }
        }

        // 보석 시스템 메타데이터 파싱

        // gem_types
        {
            if (bundle.contains("gem_types")) {
                nlohmann::json j = bundle["gem_types"];
                for (auto& e : j) {
                    GemTypeMeta gt;
                    gt.id = e.value("id", 0);
                    gt.type = e.value("type", "");
                    gt.display_name = e.value("display_name", "");
                    gt.description = e.value("description", "");
                    gt.stat_key = e.value("stat_key", "");
                    gem_types_.push_back(gt);
                    gem_types_by_id_[gt.id] = gt;
                }
            }
        }

        // gem_grades
        {
            if (bundle.contains("gem_grades")) {
                nlohmann::json j = bundle["gem_grades"];
                for (auto& e : j) {
                    GemGradeMeta gg;
                    gg.id = e.value("id", 0);
                    gg.grade = e.value("grade", "");
                    gg.display_name = e.value("display_name", "");
                    gem_grades_.push_back(gg);
                    gem_grades_by_id_[gg.id] = gg;
                }
            }
        }

        // gem_definitions
        {
            if (bundle.contains("gem_definitions")) {
                nlohmann::json j = bundle["gem_definitions"];
                for (auto& e : j) {
                    GemDefinition gd;
                    gd.gem_id = e.value("gem_id", 0);
                    gd.grade_id = e.value("grade_id", 0);
                    gd.type_id = e.value("type_id", 0);
                    gd.name = e.value("name", "");
                    gd.icon = e.value("icon", "");
                    gd.stat_multiplier = e.value("stat_multiplier", 0);
                    gem_definitions_.push_back(gd);
                    gem_definitions_by_id_[gd.gem_id] = gd;
                }
            }
        }

        // gem_gacha
        {
            if (bundle.contains("gem_gacha")) {
                nlohmann::json j = bundle["gem_gacha"];
                gem_gacha_.single_pull_cost = j.value("single_pull_cost", 0);
                gem_gacha_.multi_pull_cost = j.value("multi_pull_cost", 0);
                gem_gacha_.multi_pull_count = j.value("multi_pull_count", 0);
                if (j.contains("grade_rates") && j["grade_rates"].is_array()) {
                    for (auto& e : j["grade_rates"]) {
                        GemGradeRate rate;
                        rate.grade_id = e.value("grade_id", 0);
                        rate.rate_percent = e.value("rate_percent", 0);
                        gem_gacha_.grade_rates.push_back(rate);
                    }
                }
            }
        }

        // gem_synthesis_rules
        {
            if (bundle.contains("gem_synthesis_rules")) {
                nlohmann::json j = bundle["gem_synthesis_rules"];
                for (auto& e : j) {
                    GemSynthesisRule rule;
                    rule.from_grade = e.value("from_grade", "");
                    rule.to_grade = e.value("to_grade", "");
                    rule.success_rate_percent = e.value("success_rate_percent", 0);
                    gem_synthesis_rules_.push_back(rule);
                }
            }
        }

        // gem_conversion
        {
            if (bundle.contains("gem_conversion")) {
                nlohmann::json j = bundle["gem_conversion"];
                for (auto& e : j) {
                    GemConversionCost cost;
                    cost.grade_id = e.value("grade_id", 0);
                    cost.random_cost = e.value("random_cost", 0);
                    cost.fixed_cost = e.value("fixed_cost", 0);
                    gem_conversion_costs_.push_back(cost);
                }
            }
        }

        // gem_discard
        {
            if (bundle.contains("gem_discard")) {
                nlohmann::json j = bundle["gem_discard"];
                for (auto& e : j) {
                    GemDiscardReward reward;
                    reward.grade_id = e.value("grade_id", 0);
                    reward.crystal_reward = e.value("crystal_reward", 0);
                    gem_discard_rewards_.push_back(reward);
                }
            }
        }

        // gem_inventory
        {
            if (bundle.contains("gem_inventory")) {
                nlohmann::json j = bundle["gem_inventory"];
                gem_inventory_config_.base_capacity = j.value("base_capacity", 48);
                gem_inventory_config_.max_capacity = j.value("max_capacity", 128);
                gem_inventory_config_.expand_step = j.value("expand_step", 8);
                gem_inventory_config_.expand_cost = j.value("expand_cost", 200);
            }
        }

        // gem_slot_unlock_costs
        {
            if (bundle.contains("gem_slot_unlock_costs")) {
                nlohmann::json j = bundle["gem_slot_unlock_costs"];
                for (auto& e : j) {
                    GemSlotUnlockCost cost;
                    cost.slot_index = e.value("slot_index", 0);
                    cost.unlock_cost_crystal = e.value("unlock_cost_crystal", 0);
                    gem_slot_unlock_costs_.push_back(cost);
                }
            }
        }

        return true;
    } catch (...) {
        return false;
    }
}

const PickaxeLevel* MetadataLoader::pickaxe_level(uint32_t level) const {
    auto it = pickaxe_levels_.find(level);
    if (it == pickaxe_levels_.end()) return nullptr;
    return &it->second;
}

const MineralMeta* MetadataLoader::mineral(uint32_t id) const {
    auto it = minerals_.find(id);
    if (it == minerals_.end()) return nullptr;
    return &it->second;
}

const AdTypeMeta* MetadataLoader::ad_meta(const std::string& id) const {
    auto it = ad_types_by_id_.find(id);
    if (it == ad_types_by_id_.end()) return nullptr;
    return &it->second;
}

const GemTypeMeta* MetadataLoader::gem_type(uint32_t id) const {
    auto it = gem_types_by_id_.find(id);
    if (it == gem_types_by_id_.end()) return nullptr;
    return &it->second;
}

const GemGradeMeta* MetadataLoader::gem_grade(uint32_t id) const {
    auto it = gem_grades_by_id_.find(id);
    if (it == gem_grades_by_id_.end()) return nullptr;
    return &it->second;
}

const GemDefinition* MetadataLoader::gem_definition(uint32_t gem_id) const {
    auto it = gem_definitions_by_id_.find(gem_id);
    if (it == gem_definitions_by_id_.end()) return nullptr;
    return &it->second;
}
