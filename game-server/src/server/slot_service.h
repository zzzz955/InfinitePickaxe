#pragma once
#include "game.pb.h"
#include "slot_repository.h"
#include "metadata/metadata_loader.h"

class SlotService {
public:
    SlotService(SlotRepository& repo, const MetadataLoader& meta)
        : repo_(repo), meta_(meta) {}
    infinitepickaxe::SlotUnlockResult handle_unlock(uint32_t slot_index) const;
private:
    SlotRepository& repo_;
    const MetadataLoader& meta_;
};
