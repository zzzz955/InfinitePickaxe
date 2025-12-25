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

class GemRepository {
public:
    explicit GemRepository(ConnectionPool& pool) : pool_(pool) {}

    // 특정 곡괭이 슬롯의 보석 슬롯 6개 조회 (해금 상태 + 장착된 보석)
    std::vector<GemSlotData> get_gem_slots_for_pickaxe(const std::string& pickaxe_slot_id);

private:
    ConnectionPool& pool_;
};
