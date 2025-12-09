#include "metadata_loader.h"
#include <fstream>
#include <sstream>
#include <nlohmann/json.hpp>

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
                // 간단한 풀 구조만 flatten (index 순차 증가)
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
