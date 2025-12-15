#include "mining_repository.h"
#include <pqxx/pqxx>
#include <spdlog/spdlog.h>

MiningRepository::CompletionResult MiningRepository::record_completion(const std::string& user_id, uint32_t mineral_id, uint64_t gold_earned) {
    CompletionResult result;
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        // 골드 지급 및 카운트 증가
        auto r = tx.exec_params1(
            "UPDATE game_schema.user_game_data "
            "SET gold = gold + $2, total_mining_count = total_mining_count + 1, updated_at = NOW() "
            "WHERE user_id = $1 "
            "RETURNING gold, total_mining_count",
            user_id, static_cast<int64_t>(gold_earned));
        result.total_gold = r[0].as<int64_t>();
        result.mining_count = r[1].as<int64_t>();

        tx.commit();
    } catch (const std::exception& ex) {
        spdlog::error("record_completion failed for user {}: {}", user_id, ex.what());
    }
    return result;
}
