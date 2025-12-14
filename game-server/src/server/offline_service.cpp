#include "offline_service.h"
#include <ctime>

infinitepickaxe::OfflineRewardResult OfflineService::handle_request() const {
    // 스텁: 메타를 아직 사용하지 않음
    infinitepickaxe::OfflineRewardResult res;
    res.set_elapsed_seconds(3600);
    res.set_gold_earned(100);
    res.set_mining_count(10);
    res.set_total_gold(100);
    return res;
}
