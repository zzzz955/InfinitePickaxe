#pragma once
#include "game.pb.h"
#include "mission_repository.h"
#include "metadata/metadata_loader.h"

class MissionService {
public:
    MissionService(MissionRepository& repo, const MetadataLoader& meta)
        : repo_(repo), meta_(meta) {}
    infinitepickaxe::MissionUpdate build_stub_update() const;
private:
    MissionRepository& repo_;
    const MetadataLoader& meta_;
};
