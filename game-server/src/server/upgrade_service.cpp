#include "upgrade_service.h"
#include <ctime>

infinitepickaxe::UpgradeResult UpgradeService::handle_upgrade(const std::string& user_id, uint32_t slot_index, uint32_t target_level) const {
    infinitepickaxe::UpgradeResult res;
    res.set_slot_index(slot_index);

    // 메타데이터에서 비용/스탯 확보
    const auto* pl = meta_.pickaxe_level(target_level);
    if (!pl) {
        res.set_success(false);
        res.set_error_code("3003"); // INVALID_LEVEL
        return res;
    }

    uint64_t dps = pl->dps;
    uint64_t cost = pl->cost;

    const auto& rules = meta_.upgrade_rules();
    auto repo_result = repo_.try_upgrade_with_probability(user_id, slot_index, target_level, dps, cost, rules);

    res.set_success(repo_result.success);
    res.set_new_level(repo_result.final_level);
    res.set_new_dps(repo_result.final_dps);
    res.set_gold_spent(repo_result.insufficient_gold ? 0 : cost);
    res.set_remaining_gold(repo_result.remaining_gold);
    res.set_base_rate(repo_result.base_rate);
    res.set_bonus_rate(rules.bonus_rate);
    res.set_final_rate(repo_result.final_rate);
    res.set_pity_bonus(repo_result.pity_bonus);

    if (repo_result.invalid_slot) {
        res.set_error_code("3004"); // SLOT_NOT_FOUND
    } else if (repo_result.invalid_target) {
        res.set_error_code("3002"); // INVALID_TARGET_LEVEL
    } else if (repo_result.insufficient_gold) {
        res.set_error_code("3001"); // INSUFFICIENT_GOLD
    } else if (!repo_result.success) {
        res.set_error_code("3000"); // UPGRADE_FAILED
    } else {
        res.set_error_code("");
    }

    return res;
}
