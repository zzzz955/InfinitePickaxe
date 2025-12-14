#include "metadata_loader.h"
#include <fstream>
#include <nlohmann/json.hpp>
#include <sstream>

bool MetadataLoader::load(const std::string& base_path) {
    try {
        // pickaxe_levels.json
        {
            std::ifstream f(base_path + "/pickaxe_levels.json");
            nlohmann::json j;
            f >> j;
            for (auto& e : j) {
                PickaxeLevel pl;
                pl.level = e["level"].get<uint32_t>();
                // tier가 "T1" 포맷이면 숫자만 추출
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
                for (auto& e : j) {
                    MissionMeta m;
                    m.index = e.value("index", 0);
                    m.type = e.value("type", "");
                    m.target = e.value("target", 0);
                    m.reward_crystal = e.value("reward_crystal", 0);
                    m.description = e.value("description", "");
                    missions_.push_back(m);
                }
            } else if (j.contains("pools")) {
                // 간단하게 풀 구조를 flatten (index 순차 증가)
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

        // upgrade_rules.json (강화)
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
                // 기본값: T1 1.0, T2 0.95, T3 0.90, T4 0.85
                upgrade_rules_.base_rate_by_tier[1] = 1.0;
                upgrade_rules_.base_rate_by_tier[2] = 0.95;
                upgrade_rules_.base_rate_by_tier[3] = 0.90;
                upgrade_rules_.base_rate_by_tier[4] = 0.85;
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
