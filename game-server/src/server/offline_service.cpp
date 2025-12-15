#include "offline_service.h"

OfflineState OfflineService::get_state(const std::string& user_id) {
    uint32_t initial_seconds = meta_.offline_defaults().initial_offline_seconds;
    return repo_.get_or_create_state(user_id, initial_seconds);
}

infinitepickaxe::OfflineRewardResult OfflineService::handle_request(const std::string& user_id) {
    // TODO: 오프라인 전투 보상 계산 로직 추가
    auto state = get_state(user_id);
    infinitepickaxe::OfflineRewardResult res;
    res.set_elapsed_seconds(state.current_offline_seconds);
    res.set_gold_earned(0);
    res.set_mining_count(0);
    res.set_total_gold(0);
    return res;
}
