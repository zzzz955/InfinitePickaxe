#pragma once
#include "connection_pool.h"
#include "metadata/metadata_loader.h"
#include <string>

class UpgradeRepository {
public:
    explicit UpgradeRepository(ConnectionPool& pool) : pool_(pool) {}

    struct UpgradeAttemptResult {
        bool success{false};
        bool insufficient_gold{false};
        bool invalid_slot{false};
        bool invalid_target{false};
        uint64_t remaining_gold{0};
        uint32_t final_level{0};
        uint64_t final_dps{0};
        uint32_t tier{1};
        uint32_t pity_bonus{0}; // basis 10000
        double base_rate{0.0};
        double bonus_rate{0.0};
        double final_rate{0.0};
    };

    UpgradeAttemptResult try_upgrade_with_probability(const std::string& user_id,
                                                      uint32_t slot_index,
                                                      uint32_t target_level,
                                                      uint64_t target_dps,
                                                      uint64_t cost,
                                                      const UpgradeRules& rules);
private:
    ConnectionPool& pool_;
};
