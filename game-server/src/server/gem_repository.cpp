#include "gem_repository.h"
#include <pqxx/pqxx>
#include <spdlog/spdlog.h>
#include <chrono>

std::vector<GemSlotData> GemRepository::get_gem_slots_for_pickaxe(const std::string& pickaxe_slot_id) {
    std::vector<GemSlotData> slots;
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        // 보석 슬롯 0-5번까지 조회
        // LEFT JOIN으로 장착된 보석 정보도 함께 가져옴
        auto result = tx.exec_params(
            "SELECT "
            "  pgs.gem_slot_index, "
            "  pgs.is_unlocked, "
            "  peg.gem_instance_id, "
            "  ug.gem_id, "
            "  EXTRACT(EPOCH FROM ug.acquired_at) * 1000 AS acquired_at_ms "
            "FROM game_schema.pickaxe_gem_slots pgs "
            "LEFT JOIN game_schema.pickaxe_equipped_gems peg "
            "  ON pgs.pickaxe_slot_id = peg.pickaxe_slot_id "
            "  AND pgs.gem_slot_index = peg.gem_slot_index "
            "LEFT JOIN game_schema.user_gems ug "
            "  ON peg.gem_instance_id = ug.gem_instance_id "
            "WHERE pgs.pickaxe_slot_id = $1::uuid "
            "ORDER BY pgs.gem_slot_index",
            pickaxe_slot_id);

        for (const auto& row : result) {
            GemSlotData slot;
            slot.gem_slot_index = row[0].as<uint32_t>();
            slot.is_unlocked = row[1].as<bool>();

            // 장착된 보석이 있는 경우
            if (!row[2].is_null()) {
                GemInstanceData gem;
                gem.gem_instance_id = row[2].as<std::string>();
                gem.gem_id = row[3].as<uint32_t>();
                gem.acquired_at = row[4].as<uint64_t>();
                slot.equipped_gem = gem;
            }

            slots.push_back(slot);
        }

        tx.commit();
    } catch (const std::exception& ex) {
        spdlog::error("get_gem_slots_for_pickaxe failed for pickaxe_slot_id {}: {}",
                      pickaxe_slot_id, ex.what());
    }
    return slots;
}
