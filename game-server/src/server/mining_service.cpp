#include "mining_service.h"
#include <ctime>

infinitepickaxe::MiningUpdate MiningService::handle_start(uint32_t mineral_id) const {
    uint64_t max_hp = 1000;
    if (auto m = meta_.mineral(mineral_id)) {
        max_hp = m->hp;
    }
    infinitepickaxe::MiningUpdate upd;
    upd.set_mineral_id(mineral_id);
    upd.set_current_hp(max_hp);
    upd.set_max_hp(max_hp);
    upd.set_damage_dealt(0);
    upd.set_server_timestamp(static_cast<uint64_t>(std::time(nullptr)));
    return upd;
}

infinitepickaxe::MiningUpdate MiningService::handle_sync(uint32_t mineral_id, uint64_t client_hp) const {
    uint64_t max_hp = client_hp;
    if (auto m = meta_.mineral(mineral_id)) {
        max_hp = m->hp;
    }
    infinitepickaxe::MiningUpdate upd;
    upd.set_mineral_id(mineral_id);
    upd.set_current_hp(client_hp);
    upd.set_max_hp(max_hp);
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
