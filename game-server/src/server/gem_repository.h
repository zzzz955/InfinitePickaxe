#pragma once
#include "connection_pool.h"
#include <optional>
#include <string>
#include <vector>

// 보석 인스턴스 정보
struct GemInstanceData {
    std::string gem_instance_id;  // UUID
    uint32_t gem_id;              // 메타데이터 gem_id
    uint64_t acquired_at;         // Unix timestamp ms
};

// 보석 슬롯 정보 (해금 상태 + 장착된 보석)
struct GemSlotData {
    uint32_t gem_slot_index;           // 0-5
    bool is_unlocked;
    std::optional<GemInstanceData> equipped_gem;  // 장착된 보석 (nullable)
};

// 뽑기 트랜잭션 결과
struct GachaResult {
    bool success{false};
    bool insufficient_crystal{false};
    bool inventory_full{false};
    uint32_t remaining_crystal{0};
    std::vector<GemInstanceData> created_gems;
};

// 합성 트랜잭션 결과
struct SynthesisResult {
    bool success{false};
    bool invalid_gems{false};
    std::optional<GemInstanceData> result_gem;  // 합성 성공 시
};

// 변환 트랜잭션 결과
struct ConversionResult {
    bool success{false};
    bool insufficient_crystal{false};
    bool gem_not_found{false};
    uint32_t remaining_crystal{0};
};

// 분해 트랜잭션 결과
struct DiscardResult {
    bool success{false};
    uint32_t crystal_earned{0};
    uint32_t total_crystal{0};
};

// 슬롯 해금 트랜잭션 결과
struct GemSlotUnlockResult {
    bool success{false};
    bool already_unlocked{false};
    bool insufficient_crystal{false};
    uint32_t remaining_crystal{0};
};

// 인벤토리 확장 트랜잭션 결과
struct InventoryExpandResult {
    bool success{false};
    bool max_capacity_reached{false};
    bool insufficient_crystal{false};
    uint32_t new_capacity{0};
    uint32_t remaining_crystal{0};
};

class GemRepository {
public:
    explicit GemRepository(ConnectionPool& pool) : pool_(pool) {}

    // 조회
    std::vector<GemSlotData> get_gem_slots_for_pickaxe(const std::string& pickaxe_slot_id);
    std::vector<GemInstanceData> get_user_gems(const std::string& user_id);
    std::optional<GemInstanceData> get_gem_by_instance_id(const std::string& gem_instance_id);
    uint32_t get_inventory_capacity(const std::string& user_id);

    // 보석 생성/삭제
    std::optional<GemInstanceData> create_gem(const std::string& user_id, uint32_t gem_id);
    bool delete_gems(const std::vector<std::string>& gem_instance_ids);

    // 장착/해제
    bool equip_gem(const std::string& pickaxe_slot_id, uint32_t gem_slot_index,
                   const std::string& gem_instance_id);
    bool unequip_gem(const std::string& pickaxe_slot_id, uint32_t gem_slot_index);

    // 트랜잭션
    GachaResult gacha_pull(const std::string& user_id, uint32_t crystal_cost,
                           const std::vector<uint32_t>& gem_ids);
    SynthesisResult synthesize_gems(const std::string& user_id,
                                     const std::vector<std::string>& gem_instance_ids,
                                     uint32_t result_gem_id);
    ConversionResult convert_gem_type(const std::string& gem_instance_id,
                                       uint32_t new_gem_id, uint32_t crystal_cost);
    DiscardResult discard_gems(const std::string& user_id,
                                const std::vector<std::string>& gem_instance_ids,
                                uint32_t crystal_reward);
    GemSlotUnlockResult unlock_gem_slot(const std::string& pickaxe_slot_id,
                                         uint32_t gem_slot_index, uint32_t crystal_cost);
    InventoryExpandResult expand_inventory(const std::string& user_id, uint32_t crystal_cost);

private:
    ConnectionPool& pool_;
};
