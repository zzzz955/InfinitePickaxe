#include "mission_service.h"
#include <ctime>

infinitepickaxe::MissionUpdate MissionService::build_stub_update() const {
    infinitepickaxe::MissionUpdate upd;
    for (auto& m : meta_.missions()) {
        auto* entry = upd.add_missions();
        entry->set_index(m.index);
        entry->set_type(m.type);
        entry->set_description(m.description);
        entry->set_target(m.target);
        entry->set_current(0);
        entry->set_reward_crystal(m.reward_crystal);
        entry->set_completed(false);
        entry->set_claimed(false);
    }
    upd.set_milestone_completed_3(false);
    upd.set_milestone_completed_5(false);
    upd.set_milestone_completed_7(false);
    upd.set_offline_bonus_hours(0);
    upd.set_reset_time(static_cast<uint64_t>(std::time(nullptr)) + 86400);
    return upd;
}
