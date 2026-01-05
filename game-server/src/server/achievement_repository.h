#pragma once
#include "connection_pool.h"
#include <string>
#include <vector>
#include <optional>

struct AchievementCounter {
    std::string user_id;
    std::string achievement_type;
    uint64_t current_value{0};
};

struct AchievementChain {
    std::string user_id;
    uint32_t chain_id{0};
    uint32_t last_claimed_step{0};
};

class AchievementRepository {
public:
    explicit AchievementRepository(ConnectionPool& pool) : pool_(pool) {}

    std::optional<uint64_t> add_progress(const std::string& user_id,
                                         const std::string& achievement_type,
                                         uint64_t delta);
    std::optional<uint64_t> get_progress(const std::string& user_id,
                                         const std::string& achievement_type);
    std::optional<uint32_t> get_last_claimed_step(const std::string& user_id,
                                                  uint32_t chain_id);
    bool try_claim_chain_step(const std::string& user_id,
                              uint32_t chain_id,
                              uint32_t expected_prev_step,
                              uint32_t new_step);
    std::vector<AchievementCounter> get_all_counters(const std::string& user_id);
    std::vector<AchievementChain> get_all_chains(const std::string& user_id);

private:
    ConnectionPool& pool_;
};
