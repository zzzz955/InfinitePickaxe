#pragma once
#include <string>
#include <unordered_map>
#include <vector>
#include <optional>

struct PickaxeLevel {
    uint32_t level;
    uint32_t tier;
    uint64_t attack_power;
    double attack_speed;     // 1.0 = 1 attack per second
    uint64_t dps;            // attack_power * attack_speed (base)
    uint64_t cost;
};

struct MineralMeta {
    uint32_t id;
    std::string name;
    uint64_t hp;
    uint64_t reward;
    uint32_t respawn_time; // seconds
    uint64_t recommended_min_dps{0};
    uint64_t recommended_max_dps{0};
};

struct MissionMeta {
    uint32_t index{0};
    uint32_t id{0};
    std::string type;
    uint32_t target;
    uint32_t reward_crystal;
    std::string description;
    std::string difficulty;
    std::optional<uint32_t> mineral_id;
};

struct MilestoneBonus {
    uint32_t completed;
    uint32_t bonus_hours;
};

struct AdTypeMeta {
    std::string id;
    std::string effect;
    uint32_t daily_limit{0};
    std::vector<uint32_t> rewards_by_view;
    uint32_t cost_multiplier{100};          // basis 100 (예: 75 = 0.75x)
    bool apply_to_all_slots{true};          // mission_reroll용 파라미터
    bool progress_reset_on_reroll{true};    // mission_reroll 진행도 초기화 여부
};

struct MissionRerollMeta {
    uint32_t free_rerolls_per_day{0};
    uint32_t ad_rerolls_per_day{0};
    bool apply_to_all_slots{true};
    bool progress_reset_on_reroll{true};
};

struct OfflineDefaults {
    uint32_t initial_offline_seconds{0}; // hours -> seconds 변환 저장
};

struct UpgradeRules {
    double min_rate = 0.3;   // 30%
    double bonus_rate = 0.1; // additive bonus rate
    std::unordered_map<uint32_t, double> base_rate_by_tier; // tier -> base prob (0.0~1.0)

    double base_rate(uint32_t tier) const {
        auto it = base_rate_by_tier.find(tier);
        if (it != base_rate_by_tier.end()) return it->second;
        return 1.0;
    }
};

class MetadataLoader {
public:
    bool load(const std::string& base_path);

    const PickaxeLevel* pickaxe_level(uint32_t level) const;
    const MineralMeta* mineral(uint32_t id) const;
    const std::vector<MissionMeta>& missions() const { return missions_; }
    const std::vector<MilestoneBonus>& milestone_bonuses() const { return milestone_bonuses_; }
    const std::vector<AdTypeMeta>& ad_types() const { return ad_types_; }
    const AdTypeMeta* ad_meta(const std::string& id) const;
    const MissionRerollMeta& mission_reroll() const { return mission_reroll_; }
    const OfflineDefaults& offline_defaults() const { return offline_defaults_; }
    const UpgradeRules& upgrade_rules() const { return upgrade_rules_; }

private:
    std::unordered_map<uint32_t, PickaxeLevel> pickaxe_levels_;
    std::unordered_map<uint32_t, MineralMeta> minerals_;
    std::vector<MissionMeta> missions_;
    std::vector<MilestoneBonus> milestone_bonuses_;
    std::vector<AdTypeMeta> ad_types_;
    std::unordered_map<std::string, AdTypeMeta> ad_types_by_id_;
    MissionRerollMeta mission_reroll_;
    OfflineDefaults offline_defaults_;
    UpgradeRules upgrade_rules_;
};
