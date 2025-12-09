#include "upgrade_service.h"
#include <ctime>

infinitepickaxe::UpgradeResult UpgradeService::handle_upgrade(uint32_t slot_index, uint32_t target_level) const {
    infinitepickaxe::UpgradeResult res;
    res.set_success(true);
    res.set_slot_index(slot_index);
    res.set_new_level(target_level);
    uint64_t dps = 0;
    uint64_t cost = 0;
    if (auto pl = meta_.pickaxe_level(target_level)) {
        dps = pl->dps;
        cost = pl->cost;
    }
    res.set_new_dps(dps);
    res.set_gold_spent(cost);
    res.set_remaining_gold(0); // 스텁
    res.set_error_code("");
    return res;
}
