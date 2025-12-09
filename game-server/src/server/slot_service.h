#pragma once
#include "game.pb.h"
#include "slot_repository.h"

class SlotService {
public:
    explicit SlotService(SlotRepository& repo) : repo_(repo) {}
    infinitepickaxe::SlotUnlockResult handle_unlock(uint32_t slot_index) const;
private:
    SlotRepository& repo_;
};
