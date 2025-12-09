#pragma once
#include "connection_pool.h"

class UpgradeRepository {
public:
    explicit UpgradeRepository(ConnectionPool& pool) : pool_(pool) {}

    struct UpgradeResult {
        bool success{false};
        uint64_t remaining_gold{0};
    };

    UpgradeResult try_upgrade(const std::string& user_id,
                              uint32_t slot_index,
                              uint64_t cost,
                              uint32_t new_level,
                              uint64_t new_dps);
private:
    ConnectionPool& pool_;
};
