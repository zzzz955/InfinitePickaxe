#pragma once
#include "game.pb.h"
#include <string>
#include "mining_repository.h"

class MiningService {
public:
    explicit MiningService(MiningRepository& repo) : repo_(repo) {}
    infinitepickaxe::MiningUpdate handle_start(uint32_t mineral_id) const;
    infinitepickaxe::MiningUpdate handle_sync(uint32_t mineral_id, uint64_t client_hp) const;
    infinitepickaxe::MiningUpdate handle_complete(uint32_t mineral_id) const;
private:
    MiningRepository& repo_;
};
