#pragma once
#include "connection_pool.h"

class MiningRepository {
public:
    explicit MiningRepository(ConnectionPool& pool) : pool_(pool) {}

    struct CompletionResult {
        uint64_t total_gold{0};
        uint64_t mining_count{0};
    };

    CompletionResult record_completion(const std::string& user_id, uint32_t mineral_id, uint64_t gold_earned);
    CompletionResult apply_offline_reward(const std::string& user_id, uint64_t gold_earned, uint32_t mining_count);

private:
    ConnectionPool& pool_;
};
