#pragma once
#include "game.pb.h"
#include "slot_repository.h"
#include "game_repository.h"
#include "metadata/metadata_loader.h"
#include <string>

class SlotService {
public:
    SlotService(SlotRepository& repo, GameRepository& game_repo, const MetadataLoader& meta)
        : repo_(repo), game_repo_(game_repo), meta_(meta) {}

    // 모든 슬롯 정보 조회
    infinitepickaxe::AllSlotsResponse handle_all_slots(const std::string& user_id) const;

    // 슬롯 해금
    infinitepickaxe::SlotUnlockResult handle_unlock(const std::string& user_id, uint32_t slot_index) const;

    // DPS 계산 헬퍼
    uint64_t calculate_total_dps(const std::vector<PickaxeSlot>& slots) const;

private:
    SlotRepository& repo_;
    GameRepository& game_repo_;
    const MetadataLoader& meta_;
};
