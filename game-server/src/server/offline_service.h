#pragma once
#include "game.pb.h"
#include "offline_repository.h"
#include "metadata/metadata_loader.h"

class OfflineService {
public:
    OfflineService(OfflineRepository& repo, const MetadataLoader& meta)
        : repo_(repo), meta_(meta) {}
    infinitepickaxe::OfflineRewardResult handle_request() const;
private:
    OfflineRepository& repo_;
    const MetadataLoader& meta_;
};
