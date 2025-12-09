#include "upgrade_service.h"
#include <ctime>

infinitepickaxe::UpgradeResult UpgradeService::handle_upgrade(const std::string& user_id, uint32_t slot_index, uint32_t target_level) const {
    infinitepickaxe::UpgradeResult res;
    res.set_slot_index(slot_index);
    res.set_new_level(target_level);
    uint64_t dps = 0;
    uint64_t cost = 0;
    if (auto pl = meta_.pickaxe_level(target_level)) {
        dps = pl->dps;
        cost = pl->cost;
    }
    auto repo_result = repo_.try_upgrade(user_id, slot_index, cost, target_level, dps);
    res.set_new_dps(dps);
    res.set_gold_spent(cost);
    res.set_remaining_gold(repo_result.remaining_gold);
    res.set_success(repo_result.success);
    if (!repo_result.success) {
        res.set_error_code("3001"); // INSUFFICIENT_GOLD
    } else {
        res.set_error_code("");
    }
    return res;
}
