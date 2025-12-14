#include "mission_service.h"
#include <ctime>

infinitepickaxe::DailyMissionsResponse MissionService::build_stub_update() const {
    infinitepickaxe::DailyMissionsResponse res;
    for (auto& m : meta_.missions()) {
        auto* entry = res.add_missions();
        entry->set_mission_id(m.index);
        entry->set_type(m.type);
        entry->set_description(m.description);
        entry->set_required_progress(m.target);
        entry->set_current_progress(0);
        entry->set_gold_reward(m.reward_crystal);
        entry->set_is_completed(false);
        entry->set_is_claimed(false);
    }
    res.set_ads_watched_today(0);
    res.set_mission_rerolls_used(0);
    return res;
}
