#pragma once
#include "game.pb.h"
#include <string>
#include "mining_repository.h"
#include "slot_repository.h"
#include "metadata/metadata_loader.h"

class MiningService {
public:
    MiningService(MiningRepository& repo, SlotRepository& slot_repo, const MetadataLoader& meta)
        : repo_(repo), slot_repo_(slot_repo), meta_(meta) {}

    // 서버 권위형 아키텍처로 변경되어 더 이상 사용하지 않음
    // infinitepickaxe::MiningUpdate handle_start(const std::string& user_id, uint32_t mineral_id) const;
    // infinitepickaxe::MiningUpdate handle_sync(const std::string& user_id, uint32_t mineral_id, uint64_t client_hp) const;

    infinitepickaxe::MiningComplete handle_complete(const std::string& user_id, uint32_t mineral_id) const;

private:
    // 유저의 총 DPS 계산 (모든 슬롯 합계)
    uint64_t calculate_user_dps(const std::string& user_id) const;

    MiningRepository& repo_;
    SlotRepository& slot_repo_;
    const MetadataLoader& meta_;
};
