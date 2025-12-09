#pragma once
#include "game.pb.h"
#include "mission_repository.h"

class MissionService {
public:
    explicit MissionService(MissionRepository& repo) : repo_(repo) {}
    infinitepickaxe::MissionUpdate build_stub_update() const;
private:
    MissionRepository& repo_;
};
