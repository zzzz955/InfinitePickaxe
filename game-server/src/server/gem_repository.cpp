#include "gem_repository.h"
#include <pqxx/pqxx>
#include <spdlog/spdlog.h>
#include <chrono>

std::vector<GemSlotData> GemRepository::get_gem_slots_for_pickaxe(const std::string& pickaxe_slot_id) {
    std::vector<GemSlotData> slots;
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        auto result = tx.exec_params(
            "SELECT "
            "  pgs.gem_slot_index, "
            "  pgs.is_unlocked, "
            "  peg.gem_instance_id, "
            "  ug.gem_id, "
            "  FLOOR(EXTRACT(EPOCH FROM ug.acquired_at) * 1000)::BIGINT AS acquired_at_ms "
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
        spdlog::error("get_gem_slots_for_pickaxe failed: {}", ex.what());
    }
    return slots;
}

std::vector<GemInstanceData> GemRepository::get_user_gems(const std::string& user_id) {
    std::vector<GemInstanceData> gems;
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        auto result = tx.exec_params(
            "SELECT gem_instance_id, gem_id, "
            "  FLOOR(EXTRACT(EPOCH FROM acquired_at) * 1000)::BIGINT AS acquired_at_ms "
            "FROM game_schema.user_gems "
            "WHERE user_id = $1::uuid "
            "ORDER BY acquired_at DESC",
            user_id);

        for (const auto& row : result) {
            GemInstanceData gem;
            gem.gem_instance_id = row[0].as<std::string>();
            gem.gem_id = row[1].as<uint32_t>();
            gem.acquired_at = row[2].as<uint64_t>();
            gems.push_back(gem);
        }

        tx.commit();
    } catch (const std::exception& ex) {
        spdlog::error("get_user_gems failed: {}", ex.what());
    }
    return gems;
}

std::optional<GemInstanceData> GemRepository::get_gem_by_instance_id(const std::string& gem_instance_id) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        auto result = tx.exec_params(
            "SELECT gem_instance_id, gem_id, "
            "  FLOOR(EXTRACT(EPOCH FROM acquired_at) * 1000)::BIGINT AS acquired_at_ms "
            "FROM game_schema.user_gems "
            "WHERE gem_instance_id = $1::uuid",
            gem_instance_id);

        if (result.empty()) {
            return std::nullopt;
        }

        GemInstanceData gem;
        gem.gem_instance_id = result[0][0].as<std::string>();
        gem.gem_id = result[0][1].as<uint32_t>();
        gem.acquired_at = result[0][2].as<uint64_t>();

        tx.commit();
        return gem;
    } catch (const std::exception& ex) {
        spdlog::error("get_gem_by_instance_id failed: {}", ex.what());
        return std::nullopt;
    }
}

uint32_t GemRepository::get_inventory_capacity(const std::string& user_id) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        auto row = tx.exec_params1(
            "SELECT current_capacity FROM game_schema.user_gem_inventory "
            "WHERE user_id = $1::uuid",
            user_id);

        uint32_t capacity = row[0].as<uint32_t>();
        tx.commit();
        return capacity;
    } catch (const std::exception& ex) {
        spdlog::error("get_inventory_capacity failed: {}", ex.what());
        return 48;  // 기본값
    }
}

std::optional<GemInstanceData> GemRepository::create_gem(const std::string& user_id, uint32_t gem_id) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        auto result = tx.exec_params(
            "INSERT INTO game_schema.user_gems (user_id, gem_id) "
            "VALUES ($1::uuid, $2) "
            "RETURNING gem_instance_id, gem_id, "
            "  FLOOR(EXTRACT(EPOCH FROM acquired_at) * 1000)::BIGINT AS acquired_at_ms",
            user_id, static_cast<int32_t>(gem_id));

        if (result.empty()) {
            return std::nullopt;
        }

        GemInstanceData gem;
        gem.gem_instance_id = result[0][0].as<std::string>();
        gem.gem_id = result[0][1].as<uint32_t>();
        gem.acquired_at = result[0][2].as<uint64_t>();

        tx.commit();
        return gem;
    } catch (const std::exception& ex) {
        spdlog::error("create_gem failed: {}", ex.what());
        return std::nullopt;
    }
}

bool GemRepository::delete_gems(const std::vector<std::string>& gem_instance_ids) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        for (const auto& id : gem_instance_ids) {
            tx.exec_params(
                "DELETE FROM game_schema.user_gems WHERE gem_instance_id = $1::uuid",
                id);
        }

        tx.commit();
        return true;
    } catch (const std::exception& ex) {
        spdlog::error("delete_gems failed: {}", ex.what());
        return false;
    }
}

bool GemRepository::equip_gem(const std::string& pickaxe_slot_id, uint32_t gem_slot_index,
                               const std::string& gem_instance_id) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        tx.exec_params(
            "INSERT INTO game_schema.pickaxe_equipped_gems "
            "(pickaxe_slot_id, gem_slot_index, gem_instance_id) "
            "VALUES ($1::uuid, $2, $3::uuid) "
            "ON CONFLICT (pickaxe_slot_id, gem_slot_index) "
            "DO UPDATE SET gem_instance_id = EXCLUDED.gem_instance_id",
            pickaxe_slot_id, static_cast<int32_t>(gem_slot_index), gem_instance_id);

        tx.commit();
        return true;
    } catch (const std::exception& ex) {
        spdlog::error("equip_gem failed: {}", ex.what());
        return false;
    }
}

bool GemRepository::unequip_gem(const std::string& pickaxe_slot_id, uint32_t gem_slot_index) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        tx.exec_params(
            "DELETE FROM game_schema.pickaxe_equipped_gems "
            "WHERE pickaxe_slot_id = $1::uuid AND gem_slot_index = $2",
            pickaxe_slot_id, static_cast<int32_t>(gem_slot_index));

        tx.commit();
        return true;
    } catch (const std::exception& ex) {
        spdlog::error("unequip_gem failed: {}", ex.what());
        return false;
    }
}

GachaResult GemRepository::gacha_pull(const std::string& user_id, uint32_t crystal_cost,
                                       const std::vector<uint32_t>& gem_ids) {
    GachaResult result;
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        // 인벤토리 용량 확인
        auto inv_row = tx.exec_params1(
            "SELECT current_capacity FROM game_schema.user_gem_inventory WHERE user_id = $1::uuid",
            user_id);
        uint32_t capacity = inv_row[0].as<uint32_t>();

        auto count_row = tx.exec_params1(
            "SELECT COUNT(*) FROM game_schema.user_gems WHERE user_id = $1::uuid",
            user_id);
        uint32_t current_count = count_row[0].as<uint32_t>();

        if (current_count + gem_ids.size() > capacity) {
            result.inventory_full = true;
            return result;
        }

        // 크리스탈 차감
        auto crystal_row = tx.exec_params(
            "UPDATE game_schema.user_game_data "
            "SET crystal = crystal - $2 "
            "WHERE user_id = $1::uuid AND crystal >= $2 "
            "RETURNING crystal",
            user_id, static_cast<int32_t>(crystal_cost));

        if (crystal_row.empty()) {
            result.insufficient_crystal = true;
            return result;
        }

        result.remaining_crystal = crystal_row[0][0].as<uint32_t>();

        // 보석 생성
        for (uint32_t gem_id : gem_ids) {
            auto gem_row = tx.exec_params(
                "INSERT INTO game_schema.user_gems (user_id, gem_id) "
                "VALUES ($1::uuid, $2) "
                "RETURNING gem_instance_id, gem_id, "
                "  FLOOR(EXTRACT(EPOCH FROM acquired_at) * 1000)::BIGINT AS acquired_at_ms",
                user_id, static_cast<int32_t>(gem_id));

            GemInstanceData gem;
            gem.gem_instance_id = gem_row[0][0].as<std::string>();
            gem.gem_id = gem_row[0][1].as<uint32_t>();
            gem.acquired_at = gem_row[0][2].as<uint64_t>();
            result.created_gems.push_back(gem);
        }

        tx.commit();
        result.success = true;
    } catch (const std::exception& ex) {
        spdlog::error("gacha_pull failed: {}", ex.what());
        result.success = false;
    }
    return result;
}

SynthesisResult GemRepository::synthesize_gems(const std::string& user_id,
                                                const std::vector<std::string>& gem_instance_ids,
                                                uint32_t result_gem_id) {
    SynthesisResult result;
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        // 3개 보석 소유 확인
        auto check_result = tx.exec_params(
            "SELECT COUNT(*) FROM game_schema.user_gems "
            "WHERE user_id = $1::uuid AND gem_instance_id = ANY($2::uuid[])",
            user_id, gem_instance_ids);

        if (check_result[0][0].as<uint32_t>() != 3) {
            result.invalid_gems = true;
            return result;
        }

        // 3개 보석 삭제
        for (const auto& id : gem_instance_ids) {
            tx.exec_params(
                "DELETE FROM game_schema.user_gems WHERE gem_instance_id = $1::uuid",
                id);
        }

        // 합성 성공 시 새 보석 생성
        if (result_gem_id > 0) {
            auto gem_row = tx.exec_params(
                "INSERT INTO game_schema.user_gems (user_id, gem_id) "
                "VALUES ($1::uuid, $2) "
                "RETURNING gem_instance_id, gem_id, "
                "  FLOOR(EXTRACT(EPOCH FROM acquired_at) * 1000)::BIGINT AS acquired_at_ms",
                user_id, static_cast<int32_t>(result_gem_id));

            GemInstanceData gem;
            gem.gem_instance_id = gem_row[0][0].as<std::string>();
            gem.gem_id = gem_row[0][1].as<uint32_t>();
            gem.acquired_at = gem_row[0][2].as<uint64_t>();
            result.result_gem = gem;
        }

        tx.commit();
        result.success = true;
    } catch (const std::exception& ex) {
        spdlog::error("synthesize_gems failed: {}", ex.what());
        result.success = false;
    }
    return result;
}

ConversionResult GemRepository::convert_gem_type(const std::string& gem_instance_id,
                                                  uint32_t new_gem_id, uint32_t crystal_cost) {
    ConversionResult result;
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        // 보석 소유자 조회
        auto gem_row = tx.exec_params(
            "SELECT user_id FROM game_schema.user_gems WHERE gem_instance_id = $1::uuid",
            gem_instance_id);

        if (gem_row.empty()) {
            result.gem_not_found = true;
            return result;
        }

        std::string user_id = gem_row[0][0].as<std::string>();

        // 크리스탈 차감
        auto crystal_row = tx.exec_params(
            "UPDATE game_schema.user_game_data "
            "SET crystal = crystal - $2 "
            "WHERE user_id = $1::uuid AND crystal >= $2 "
            "RETURNING crystal",
            user_id, static_cast<int32_t>(crystal_cost));

        if (crystal_row.empty()) {
            result.insufficient_crystal = true;
            return result;
        }

        result.remaining_crystal = crystal_row[0][0].as<uint32_t>();

        // 보석 타입 변경
        tx.exec_params(
            "UPDATE game_schema.user_gems "
            "SET gem_id = $2 "
            "WHERE gem_instance_id = $1::uuid",
            gem_instance_id, static_cast<int32_t>(new_gem_id));

        tx.commit();
        result.success = true;
    } catch (const std::exception& ex) {
        spdlog::error("convert_gem_type failed: {}", ex.what());
        result.success = false;
    }
    return result;
}

DiscardResult GemRepository::discard_gems(const std::string& user_id,
                                           const std::vector<std::string>& gem_instance_ids,
                                           uint32_t crystal_reward) {
    DiscardResult result;
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        // 보석 삭제
        for (const auto& id : gem_instance_ids) {
            tx.exec_params(
                "DELETE FROM game_schema.user_gems "
                "WHERE gem_instance_id = $1::uuid AND user_id = $2::uuid",
                id, user_id);
        }

        // 크리스탈 지급
        auto crystal_row = tx.exec_params(
            "UPDATE game_schema.user_game_data "
            "SET crystal = crystal + $2 "
            "WHERE user_id = $1::uuid "
            "RETURNING crystal",
            user_id, static_cast<int32_t>(crystal_reward));

        if (!crystal_row.empty()) {
            result.crystal_earned = crystal_reward;
            result.total_crystal = crystal_row[0][0].as<uint32_t>();
            result.success = true;
        }

        tx.commit();
    } catch (const std::exception& ex) {
        spdlog::error("discard_gems failed: {}", ex.what());
        result.success = false;
    }
    return result;
}

GemSlotUnlockResult GemRepository::unlock_gem_slot(const std::string& pickaxe_slot_id,
                                                     uint32_t gem_slot_index, uint32_t crystal_cost) {
    GemSlotUnlockResult result;
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        // 이미 해금되었는지 확인
        auto check_result = tx.exec_params(
            "SELECT is_unlocked FROM game_schema.pickaxe_gem_slots "
            "WHERE pickaxe_slot_id = $1::uuid AND gem_slot_index = $2",
            pickaxe_slot_id, static_cast<int32_t>(gem_slot_index));

        if (!check_result.empty() && check_result[0][0].as<bool>()) {
            result.already_unlocked = true;
            return result;
        }

        // 곡괭이 소유자 조회
        auto owner_row = tx.exec_params(
            "SELECT user_id FROM game_schema.pickaxe_slots WHERE slot_id = $1::uuid",
            pickaxe_slot_id);

        if (owner_row.empty()) {
            return result;
        }

        std::string user_id = owner_row[0][0].as<std::string>();

        // 크리스탈 차감
        auto crystal_row = tx.exec_params(
            "UPDATE game_schema.user_game_data "
            "SET crystal = crystal - $2 "
            "WHERE user_id = $1::uuid AND crystal >= $2 "
            "RETURNING crystal",
            user_id, static_cast<int32_t>(crystal_cost));

        if (crystal_row.empty()) {
            result.insufficient_crystal = true;
            return result;
        }

        result.remaining_crystal = crystal_row[0][0].as<uint32_t>();

        // 슬롯 해금
        tx.exec_params(
            "INSERT INTO game_schema.pickaxe_gem_slots "
            "(pickaxe_slot_id, gem_slot_index, is_unlocked, unlocked_at) "
            "VALUES ($1::uuid, $2, TRUE, NOW()) "
            "ON CONFLICT (pickaxe_slot_id, gem_slot_index) "
            "DO UPDATE SET is_unlocked = TRUE, unlocked_at = NOW()",
            pickaxe_slot_id, static_cast<int32_t>(gem_slot_index));

        tx.commit();
        result.success = true;
    } catch (const std::exception& ex) {
        spdlog::error("unlock_gem_slot failed: {}", ex.what());
        result.success = false;
    }
    return result;
}

InventoryExpandResult GemRepository::expand_inventory(const std::string& user_id, uint32_t crystal_cost) {
    InventoryExpandResult result;
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        // 현재 용량 조회
        auto inv_row = tx.exec_params1(
            "SELECT current_capacity FROM game_schema.user_gem_inventory WHERE user_id = $1::uuid",
            user_id);

        uint32_t current_capacity = inv_row[0].as<uint32_t>();

        // 최대 용량 확인 (128)
        if (current_capacity >= 128) {
            result.max_capacity_reached = true;
            return result;
        }

        // 크리스탈 차감
        auto crystal_row = tx.exec_params(
            "UPDATE game_schema.user_game_data "
            "SET crystal = crystal - $2 "
            "WHERE user_id = $1::uuid AND crystal >= $2 "
            "RETURNING crystal",
            user_id, static_cast<int32_t>(crystal_cost));

        if (crystal_row.empty()) {
            result.insufficient_crystal = true;
            return result;
        }

        result.remaining_crystal = crystal_row[0][0].as<uint32_t>();

        // 용량 확장 (8칸씩)
        uint32_t new_capacity = std::min(current_capacity + 8, 128u);

        tx.exec_params(
            "UPDATE game_schema.user_gem_inventory "
            "SET current_capacity = $2 "
            "WHERE user_id = $1::uuid",
            user_id, static_cast<int32_t>(new_capacity));

        tx.commit();
        result.success = true;
        result.new_capacity = new_capacity;
    } catch (const std::exception& ex) {
        spdlog::error("expand_inventory failed: {}", ex.what());
        result.success = false;
    }
    return result;
}
