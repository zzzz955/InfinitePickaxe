#pragma once
#include "game.pb.h"
#include "upgrade_repository.h"

class UpgradeService {
public:
    explicit UpgradeService(UpgradeRepository& repo) : repo_(repo) {}
    infinitepickaxe::UpgradeResult handle_upgrade(uint32_t slot_index, uint32_t target_level) const;
private:
    UpgradeRepository& repo_;
};
