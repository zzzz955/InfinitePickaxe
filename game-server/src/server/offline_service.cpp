#include "offline_service.h"

infinitepickaxe::OfflineReward OfflineService::handle_request() const {
    infinitepickaxe::OfflineReward res;
    res.set_offline_seconds(3600);
    res.set_gold_earned(100);
    res.set_mining_cycles(10);
    res.set_mineral_id(1);
    res.set_efficiency(1.0);
    res.set_new_total_gold(100);
    return res;
}
