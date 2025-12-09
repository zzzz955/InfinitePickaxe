#pragma once
#include "game.pb.h"

class SlotService {
public:
    infinitepickaxe::SlotUnlockResult handle_unlock(uint32_t slot_index) const;
};
