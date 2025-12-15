#include "metadata_loader.h"
#include <fstream>
#include <nlohmann/json.hpp>
#include <sstream>

bool MetadataLoader::load(const std::string& base_path) {
    try {
        pickaxe_levels_.clear();
        minerals_.clear();
        missions_.clear();
        milestone_bonuses_.clear();
        ad_types_.clear();
        ad_types_by_id_.clear();
        mission_reroll_ = MissionRerollMeta{};
        offline_defaults_ = OfflineDefaults{};

        // pickaxe_levels.json
        {
            std::ifstream f(base_path + "/pickaxe_levels.json");
            nlohmann::json j;
            f >> j;
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

        // minerals.json
        {
            std::ifstream f(base_path + "/minerals.json");
            nlohmann::json j;
            f >> j;
            if (j.is_array()) {
                for (auto& e : j) {
                    MineralMeta mm;
                    mm.id = e.value("id", 0);
                    mm.name = e.value("name", "");
                    mm.hp = e.value("hp", 0);
                    mm.reward = e.value("reward", e.value("gold", 0));
                    mm.respawn_time = e.value("respawn_time", 5);
                    minerals_[mm.id] = mm;
                }
            }
        }

        // daily_missions.json
        {
            std::ifstream f(base_path + "/daily_missions.json");
            nlohmann::json j;
            f >> j;
            if (j.is_array()) {
                uint32_t idx = 0;
                for (auto& e : j) {
                    MissionMeta m;
                    m.index = e.value("index", idx++);
                    m.type = e.value("type", "");
                    m.target = e.value("target", 0);
                    m.reward_crystal = e.value("reward_crystal", 0);
                    m.description = e.value("description", "");
                    missions_.push_back(m);
                }
            } else if (j.contains("pools")) {
                uint32_t idx = 0;
                for (auto& pool : j["pools"].items()) {
                    for (auto& e : pool.value()) {
                        MissionMeta m;
                        m.index = idx++;
                        m.type = e.value("type", "");
                        m.target = e.value("target", 0);
                        m.reward_crystal = e.value("reward_crystal", 0);
                        m.description = e.value("description", "");
                        missions_.push_back(m);
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

        // upgrade_rules.json (upgrade)
        {
            std::ifstream f(base_path + "/upgrade_rules.json");
            if (f.good()) {
                nlohmann::json j;
                f >> j;
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

        // ads.json (ad types)
        {
            std::ifstream f(base_path + "/ads.json");
            if (f.good()) {
                nlohmann::json j;
                f >> j;
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

        // offline_defaults.json
        {
            std::ifstream f(base_path + "/offline_defaults.json");
            if (f.good()) {
                nlohmann::json j;
                f >> j;
                uint32_t hours = j.value("initial_offline_hours", 0);
                offline_defaults_.initial_offline_seconds = hours * 3600;
            } else {
                offline_defaults_.initial_offline_seconds = 0;
            }
        }

        // mission_reroll.json
        {
            std::ifstream f(base_path + "/mission_reroll.json");
            if (f.good()) {
                nlohmann::json j;
                f >> j;
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
