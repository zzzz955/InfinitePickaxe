#include "mission_service.h"
#include <ctime>

infinitepickaxe::MissionUpdate MissionService::build_stub_update() const {
    infinitepickaxe::MissionUpdate upd;
    upd.set_milestone_completed_3(false);
    upd.set_milestone_completed_5(false);
    upd.set_milestone_completed_7(false);
    upd.set_offline_bonus_hours(0);
    upd.set_reset_time(static_cast<uint64_t>(std::time(nullptr)) + 86400);
    return upd;
}
