#pragma once
#include "game.pb.h"
#include "slot_repository.h"
#include "game_repository.h"
#include "gem_repository.h"
#include "metadata/metadata_loader.h"
#include <optional>
#include <string>
#include <vector>

class SlotService {
public:
    SlotService(SlotRepository& repo, GameRepository& game_repo, GemRepository& gem_repo, const MetadataLoader& meta)
        : repo_(repo), game_repo_(game_repo), gem_repo_(gem_repo), meta_(meta) {}

    infinitepickaxe::AllSlotsResponse handle_all_slots(const std::string& user_id) const;

    infinitepickaxe::SlotUnlockResult handle_unlock(const std::string& user_id, uint32_t slot_index) const;

    std::optional<PickaxeSlot> get_slot(const std::string& user_id, uint32_t slot_index) const {
        return repo_.get_slot(user_id, slot_index);
    }

    uint64_t calculate_total_dps(const std::vector<PickaxeSlot>& slots) const;

private:
    SlotRepository& repo_;
    GameRepository& game_repo_;
    GemRepository& gem_repo_;
    const MetadataLoader& meta_;
};
