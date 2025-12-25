#pragma once
#include "game.pb.h"
#include "gem_repository.h"
#include "slot_repository.h"
#include "metadata/metadata_loader.h"
#include <string>
#include <vector>

class GemService {
public:
    GemService(GemRepository& gem_repo, SlotRepository& slot_repo, const MetadataLoader& meta)
        : gem_repo_(gem_repo), slot_repo_(slot_repo), meta_(meta) {}

    // 보석 목록 조회
    infinitepickaxe::GemListResponse handle_gem_list(const std::string& user_id);

    // 가챠 (1회 또는 11회)
    infinitepickaxe::GemGachaResult handle_gacha_pull(const std::string& user_id, uint32_t pull_count);

    // 합성 (3개 → 1개, 확률)
    infinitepickaxe::GemSynthesisResult handle_synthesis(const std::string& user_id,
                                                          const std::vector<std::string>& gem_instance_ids);

    // 타입 변환 (랜덤/고정)
    infinitepickaxe::GemConversionResult handle_conversion(const std::string& user_id,
                                                            const std::string& gem_instance_id,
                                                            infinitepickaxe::GemType target_type,
                                                            bool use_fixed_cost);

    // 분해
    infinitepickaxe::GemDiscardResult handle_discard(const std::string& user_id,
                                                      const std::vector<std::string>& gem_instance_ids);

    // 장착
    infinitepickaxe::GemEquipResult handle_equip(const std::string& user_id,
                                                   uint32_t pickaxe_slot_index,
                                                   uint32_t gem_slot_index,
                                                   const std::string& gem_instance_id);

    // 해제
    infinitepickaxe::GemUnequipResult handle_unequip(const std::string& user_id,
                                                       uint32_t pickaxe_slot_index,
                                                       uint32_t gem_slot_index);

    // 보석 슬롯 해금
    infinitepickaxe::GemSlotUnlockResult handle_slot_unlock(const std::string& user_id,
                                                              uint32_t pickaxe_slot_index,
                                                              uint32_t gem_slot_index);

    // 인벤토리 확장
    infinitepickaxe::GemInventoryExpandResult handle_inventory_expand(const std::string& user_id);

private:
    GemRepository& gem_repo_;
    SlotRepository& slot_repo_;
    const MetadataLoader& meta_;

    // 가중치 랜덤 선택 (gacha용)
    uint32_t select_random_gem_by_grade_rate(const std::vector<GemGradeRate>& grade_rates);

    // GemInstanceData → GemInfo protobuf 변환
    void populate_gem_info(const GemInstanceData& gem, infinitepickaxe::GemInfo* gem_info);

    // 보석 장착/해제 시 곡괭이 스탯 보너스 계산
    PickaxeSlot calculate_pickaxe_stats_with_gems(const std::string& pickaxe_slot_id);

    // PickaxeSlot → PickaxeSlotInfo protobuf 변환 (gem_slots 포함)
    void populate_pickaxe_slot_info(const PickaxeSlot& slot, infinitepickaxe::PickaxeSlotInfo* slot_info);
};
