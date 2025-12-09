#pragma once
#include "game.pb.h"

class MissionService {
public:
    infinitepickaxe::MissionUpdate build_stub_update() const;
};
