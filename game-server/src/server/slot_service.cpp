#include "slot_service.h"

infinitepickaxe::SlotUnlockResult SlotService::handle_unlock(uint32_t slot_index) const {
    infinitepickaxe::SlotUnlockResult res;
    res.set_success(true);
    res.set_slot_index(slot_index);
    res.set_crystal_spent(0);
    res.set_remaining_crystal(0);
    res.set_error_code("");
    return res;
}
