#include "upgrade_repository.h"
#include <pqxx/pqxx>
#include <spdlog/spdlog.h>

UpgradeRepository::UpgradeResult UpgradeRepository::try_upgrade(const std::string& user_id,
                                                                uint32_t slot_index,
                                                                uint64_t cost,
                                                                uint32_t new_level,
                                                                uint64_t new_dps) {
    UpgradeResult res;
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        // 골드 차감
        auto r = tx.exec_params(
            "UPDATE game_schema.user_game_data "
            "SET gold = gold - $2, updated_at = NOW() "
            "WHERE user_id = $1 AND gold >= $2 "
            "RETURNING gold",
            user_id, static_cast<int64_t>(cost));
        if (r.empty()) {
            tx.abort();
            res.success = false;
            return res;
        }
        res.remaining_gold = r[0][0].as<int64_t>();

        // 슬롯 강화
        tx.exec_params(
            "UPDATE game_schema.pickaxe_slots "
            "SET level = $3, dps = $4, updated_at = NOW(), last_upgraded_at = NOW() "
            "WHERE user_id = $1 AND slot_index = $2",
            user_id, slot_index, new_level, static_cast<int64_t>(new_dps));

        // 최고 레벨 갱신
        tx.exec_params(
            "UPDATE game_schema.user_game_data "
            "SET highest_pickaxe_level = GREATEST(highest_pickaxe_level, $2) "
            "WHERE user_id = $1",
            user_id, new_level);

        tx.commit();
        res.success = true;
    } catch (const std::exception& ex) {
        spdlog::error("try_upgrade failed for user {} slot {}: {}", user_id, slot_index, ex.what());
    }
    return res;
}
