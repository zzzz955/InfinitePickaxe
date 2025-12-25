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

struct DailyMissionConfig {
    uint32_t total_slots{3};
    uint32_t max_daily_assign{7};
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

// 보석 시스템 메타데이터
struct GemTypeMeta {
    uint32_t id;
    std::string type;        // ATTACK_SPEED, CRIT_RATE, CRIT_DMG
    std::string display_name;
    std::string description;
    std::string stat_key;
};

struct GemGradeMeta {
    uint32_t id;
    std::string grade;       // COMMON, RARE, EPIC, HERO, LEGENDARY
    std::string display_name;
};

struct GemDefinition {
    uint32_t gem_id;
    uint32_t grade_id;
    uint32_t type_id;
    std::string name;
    std::string icon;
    uint32_t stat_multiplier; // x100 basis
};

struct GemGradeRate {
    uint32_t grade_id;
    uint32_t rate_percent;   // basis 10000 (100.00%)
};

struct GemGachaMeta {
    uint32_t single_pull_cost;
    uint32_t multi_pull_cost;
    uint32_t multi_pull_count;
    std::vector<GemGradeRate> grade_rates;
};

struct GemSynthesisRule {
    std::string from_grade;
    std::string to_grade;
    uint32_t success_rate_percent; // basis 10000
};

struct GemConversionCost {
    uint32_t grade_id;
    uint32_t random_cost;   // 크리스탈 비용 (랜덤 타입)
    uint32_t fixed_cost;    // 크리스탈 비용 (고정 타입)
};

struct GemDiscardReward {
    uint32_t grade_id;
    uint32_t crystal_reward;
};

struct GemInventoryConfig {
    uint32_t base_capacity{48};
    uint32_t max_capacity{128};
    uint32_t expand_step{8};
    uint32_t expand_cost{200};
};

struct GemSlotUnlockCost {
    uint32_t slot_index;    // 0-5
    uint32_t unlock_cost_crystal;
};

class MetadataLoader {
public:
    bool load(const std::string& base_path);

    const PickaxeLevel* pickaxe_level(uint32_t level) const;
    const MineralMeta* mineral(uint32_t id) const;
    const std::vector<MissionMeta>& missions() const { return missions_; }
    const DailyMissionConfig& daily_missions_config() const { return daily_missions_config_; }
    const std::vector<MilestoneBonus>& milestone_bonuses() const { return milestone_bonuses_; }
    const std::vector<AdTypeMeta>& ad_types() const { return ad_types_; }
    const AdTypeMeta* ad_meta(const std::string& id) const;
    const MissionRerollMeta& mission_reroll() const { return mission_reroll_; }
    const OfflineDefaults& offline_defaults() const { return offline_defaults_; }
    const UpgradeRules& upgrade_rules() const { return upgrade_rules_; }

    // 보석 시스템 getter
    const std::vector<GemTypeMeta>& gem_types() const { return gem_types_; }
    const std::vector<GemGradeMeta>& gem_grades() const { return gem_grades_; }
    const std::vector<GemDefinition>& gem_definitions() const { return gem_definitions_; }
    const GemGachaMeta& gem_gacha() const { return gem_gacha_; }
    const std::vector<GemSynthesisRule>& gem_synthesis_rules() const { return gem_synthesis_rules_; }
    const std::vector<GemConversionCost>& gem_conversion_costs() const { return gem_conversion_costs_; }
    const std::vector<GemDiscardReward>& gem_discard_rewards() const { return gem_discard_rewards_; }
    const GemInventoryConfig& gem_inventory_config() const { return gem_inventory_config_; }
    const std::vector<GemSlotUnlockCost>& gem_slot_unlock_costs() const { return gem_slot_unlock_costs_; }

    const GemTypeMeta* gem_type(uint32_t id) const;
    const GemGradeMeta* gem_grade(uint32_t id) const;
    const GemDefinition* gem_definition(uint32_t gem_id) const;

private:
    std::unordered_map<uint32_t, PickaxeLevel> pickaxe_levels_;
    std::unordered_map<uint32_t, MineralMeta> minerals_;
    std::vector<MissionMeta> missions_;
    DailyMissionConfig daily_missions_config_;
    std::vector<MilestoneBonus> milestone_bonuses_;
    std::vector<AdTypeMeta> ad_types_;
    std::unordered_map<std::string, AdTypeMeta> ad_types_by_id_;
    MissionRerollMeta mission_reroll_;
    OfflineDefaults offline_defaults_;
    UpgradeRules upgrade_rules_;

    // 보석 시스템 메타데이터
    std::vector<GemTypeMeta> gem_types_;
    std::vector<GemGradeMeta> gem_grades_;
    std::vector<GemDefinition> gem_definitions_;
    GemGachaMeta gem_gacha_;
    std::vector<GemSynthesisRule> gem_synthesis_rules_;
    std::vector<GemConversionCost> gem_conversion_costs_;
    std::vector<GemDiscardReward> gem_discard_rewards_;
    GemInventoryConfig gem_inventory_config_;
    std::vector<GemSlotUnlockCost> gem_slot_unlock_costs_;

    std::unordered_map<uint32_t, GemTypeMeta> gem_types_by_id_;
    std::unordered_map<uint32_t, GemGradeMeta> gem_grades_by_id_;
    std::unordered_map<uint32_t, GemDefinition> gem_definitions_by_id_;
};
