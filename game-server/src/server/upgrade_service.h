#pragma once
#include "game.pb.h"

class UpgradeService {
public:
    infinitepickaxe::UpgradeResult handle_upgrade(uint32_t slot_index, uint32_t target_level) const;
};
