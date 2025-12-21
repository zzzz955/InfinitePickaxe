#pragma once
#include "connection_pool.h"
#include <string>
#include <vector>
#include <chrono>

struct AdCounter {
    std::string user_id;
    std::string ad_type;
    uint32_t ad_count;
    std::chrono::system_clock::time_point reset_date;
};

class AdRepository {
public:
    explicit AdRepository(ConnectionPool& pool) : pool_(pool) {}

    AdCounter get_or_create_ad_counter(const std::string& user_id, const std::string& ad_type);
    bool increment_ad_counter(const std::string& user_id, const std::string& ad_type);
    std::vector<AdCounter> get_all_ad_counters(const std::string& user_id);

private:
    ConnectionPool& pool_;
};
