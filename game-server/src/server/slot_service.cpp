#include "slot_service.h"
#include <spdlog/spdlog.h>

infinitepickaxe::AllSlotsResponse SlotService::handle_all_slots(const std::string& user_id) const {
    infinitepickaxe::AllSlotsResponse response;

    // DB에서 슬롯 조회
    auto slots = repo_.get_user_slots(user_id);

    // protobuf 변환
    for (const auto& slot : slots) {
        auto* slot_info = response.add_slots();
        slot_info->set_slot_index(slot.slot_index);
        slot_info->set_level(slot.level);
        slot_info->set_attack_power(slot.attack_power);
        slot_info->set_attack_speed_x100(slot.attack_speed_x100);
        slot_info->set_dps(slot.dps);
        slot_info->set_is_unlocked(true); // DB에 있으면 해금된 상태
    }

    // 총 DPS 계산
    uint64_t total_dps = calculate_total_dps(slots);
    response.set_total_dps(total_dps);

    spdlog::debug("handle_all_slots: user={} slots={} total_dps={}", user_id, slots.size(), total_dps);
    return response;
}

infinitepickaxe::SlotUnlockResult SlotService::handle_unlock(const std::string& user_id, uint32_t slot_index) const {
    infinitepickaxe::SlotUnlockResult res;

    // TODO: 크리스탈 체크 및 차감 로직 (game_repository에 추가 필요)
    // 현재는 간단하게 슬롯 생성만 수행

    if (slot_index < 1 || slot_index > 3) {
        res.set_success(false);
        res.set_slot_index(slot_index);
        res.set_error_code("INVALID_SLOT_INDEX");
        return res;
    }

    // 이미 해금되었는지 확인
    auto existing = repo_.get_slot(user_id, slot_index);
    if (existing.has_value()) {
        res.set_success(false);
        res.set_slot_index(slot_index);
        res.set_error_code("ALREADY_UNLOCKED");
        return res;
    }

    // 슬롯 생성 (레벨 0, 기본 스탯)
    // critical_hit_percent=500 (5%), critical_damage=15000 (150%)
    bool created = repo_.create_slot(user_id, slot_index, 0, 1, 10, 100, 500, 15000, 10);

    if (created) {
        res.set_success(true);
        res.set_slot_index(slot_index);
        res.set_crystal_spent(0); // TODO: 실제 크리스탈 비용
        res.set_remaining_crystal(0); // TODO: 남은 크리스탈
        res.set_error_code("");
        spdlog::info("Slot unlocked: user={} slot={}", user_id, slot_index);
    } else {
        res.set_success(false);
        res.set_slot_index(slot_index);
        res.set_error_code("DB_ERROR");
    }

    return res;
}

uint64_t SlotService::calculate_total_dps(const std::vector<PickaxeSlot>& slots) const {
    uint64_t total = 0;
    for (const auto& slot : slots) {
        total += slot.dps;
    }
    return total;
}
