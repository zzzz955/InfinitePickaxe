#pragma once
#include <string>
#include <vector>
#include <unordered_map>

struct PickaxeLevel {
    uint32_t level;
    uint32_t tier;
    uint64_t dps;
    uint64_t cost;
};

struct MineralMeta {
    uint32_t id;
    std::string name;
    uint64_t hp;
    uint64_t reward;
    uint32_t respawn_time; // seconds
};

struct MissionMeta {
    uint32_t index;
    std::string type;
    uint32_t target;
    uint32_t reward_crystal;
    std::string description;
};

struct UpgradeRules {
    double min_rate = 0.3;   // 30%
    double bonus_rate = 0.1; // 기본 확률의 10%
    std::unordered_map<uint32_t, double> base_rate_by_tier; // tier -> base prob (0.0~1.0)

    double base_rate(uint32_t tier) const {
        auto it = base_rate_by_tier.find(tier);
        if (it != base_rate_by_tier.end()) return it->second;
        return 1.0; // 기본 100%
    }
};

class MetadataLoader {
public:
    bool load(const std::string& base_path);

    const PickaxeLevel* pickaxe_level(uint32_t level) const;
    const MineralMeta* mineral(uint32_t id) const;
    const std::vector<MissionMeta>& missions() const { return missions_; }
    const UpgradeRules& upgrade_rules() const { return upgrade_rules_; }

private:
    std::unordered_map<uint32_t, PickaxeLevel> pickaxe_levels_;
    std::unordered_map<uint32_t, MineralMeta> minerals_;
    std::vector<MissionMeta> missions_;
    UpgradeRules upgrade_rules_;
};
