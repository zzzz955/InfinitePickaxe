#pragma once
#include "game.pb.h"
#include "upgrade_repository.h"
#include "metadata/metadata_loader.h"

class UpgradeService {
public:
    UpgradeService(UpgradeRepository& repo, const MetadataLoader& meta)
        : repo_(repo), meta_(meta) {}
    infinitepickaxe::UpgradeResult handle_upgrade(uint32_t slot_index, uint32_t target_level) const;
private:
    UpgradeRepository& repo_;
    const MetadataLoader& meta_;
};
