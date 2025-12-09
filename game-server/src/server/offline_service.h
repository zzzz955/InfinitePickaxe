#pragma once
#include "game.pb.h"
#include "offline_repository.h"

class OfflineService {
public:
    explicit OfflineService(OfflineRepository& repo) : repo_(repo) {}
    infinitepickaxe::OfflineReward handle_request() const;
private:
    OfflineRepository& repo_;
};
