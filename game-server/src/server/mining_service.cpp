#include "mining_service.h"
#include <ctime>

infinitepickaxe::MiningUpdate MiningService::handle_start(uint32_t mineral_id) const {
    infinitepickaxe::MiningUpdate upd;
    upd.set_mineral_id(mineral_id);
    upd.set_current_hp(1000);
    upd.set_max_hp(1000);
    upd.set_damage_dealt(0);
    upd.set_server_timestamp(static_cast<uint64_t>(std::time(nullptr)));
    return upd;
}

infinitepickaxe::MiningUpdate MiningService::handle_sync(uint32_t mineral_id, uint64_t client_hp) const {
    infinitepickaxe::MiningUpdate upd;
    upd.set_mineral_id(mineral_id);
    upd.set_current_hp(client_hp);
    upd.set_max_hp(client_hp);
    upd.set_damage_dealt(0);
    upd.set_server_timestamp(static_cast<uint64_t>(std::time(nullptr)));
    return upd;
}

infinitepickaxe::MiningUpdate MiningService::handle_complete(uint32_t mineral_id) const {
    infinitepickaxe::MiningUpdate upd;
    upd.set_mineral_id(mineral_id);
    upd.set_current_hp(0);
    upd.set_max_hp(0);
    upd.set_damage_dealt(0);
    upd.set_server_timestamp(static_cast<uint64_t>(std::time(nullptr)));
    return upd;
}
