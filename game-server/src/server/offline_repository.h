#pragma once
#include "connection_pool.h"
#include <chrono>
#include <optional>
#include <string>

struct OfflineState {
    std::string user_id;
    std::chrono::system_clock::time_point offline_date;
    uint32_t current_offline_seconds{0}; // DB: seconds stored in current_offline_hours 컬럼
};

class OfflineRepository {
public:
    explicit OfflineRepository(ConnectionPool& pool) : pool_(pool) {}

    OfflineState get_or_create_state(const std::string& user_id, uint32_t initial_seconds);
    std::optional<uint32_t> add_offline_seconds(const std::string& user_id, uint32_t delta_seconds, uint32_t initial_seconds);

private:
    ConnectionPool& pool_;
};
