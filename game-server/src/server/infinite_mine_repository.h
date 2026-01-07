#pragma once

#include "connection_pool.h"
#include <string>
#include <vector>
#include <optional>

struct InfiniteMineProgress {
    uint32_t floor{0};
    std::string first_cleared_date;
    std::string last_auto_claim_date;
};

class InfiniteMineRepository {
public:
    explicit InfiniteMineRepository(ConnectionPool& pool) : pool_(pool) {}

    std::vector<InfiniteMineProgress> get_all_progress(const std::string& user_id);
    std::optional<InfiniteMineProgress> get_progress(const std::string& user_id, uint32_t floor);
    uint32_t get_highest_cleared_floor(const std::string& user_id);
    std::optional<bool> insert_first_clear(const std::string& user_id, uint32_t floor);
    bool update_auto_claim_date(const std::string& user_id, uint32_t floor, const std::string& kst_date);
    bool update_auto_claim_dates(const std::string& user_id, const std::vector<uint32_t>& floors,
                                 const std::string& kst_date);

private:
    ConnectionPool& pool_;
};
