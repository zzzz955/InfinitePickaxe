#pragma once
#include "game.pb.h"
#include <string>
#include "mining_repository.h"
#include "metadata/metadata_loader.h"

class MiningService {
public:
    MiningService(MiningRepository& repo, const MetadataLoader& meta)
        : repo_(repo), meta_(meta) {}
    infinitepickaxe::MiningUpdate handle_start(uint32_t mineral_id) const;
    infinitepickaxe::MiningUpdate handle_sync(uint32_t mineral_id, uint64_t client_hp) const;
    infinitepickaxe::MiningUpdate handle_complete(uint32_t mineral_id) const;
private:
    MiningRepository& repo_;
    const MetadataLoader& meta_;
};
