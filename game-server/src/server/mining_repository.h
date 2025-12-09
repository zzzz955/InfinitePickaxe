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

private:
    ConnectionPool& pool_;
};
