#include "upgrade_service.h"

infinitepickaxe::UpgradeResult UpgradeService::handle_upgrade(uint32_t slot_index, uint32_t target_level) const {
    infinitepickaxe::UpgradeResult res;
    res.set_success(true);
    res.set_slot_index(slot_index);
    res.set_new_level(target_level);
    res.set_new_dps(0);
    res.set_gold_spent(0);
    res.set_remaining_gold(0);
    res.set_error_code("");
    return res;
}
