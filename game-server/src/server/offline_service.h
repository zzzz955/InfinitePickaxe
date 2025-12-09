#pragma once
#include "game.pb.h"

class OfflineService {
public:
    infinitepickaxe::OfflineReward handle_request() const;
};
