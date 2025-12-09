#include "connection_pool.h"
#include <spdlog/spdlog.h>

ConnectionPool::ConnectionPool(const std::string& conn_str, std::size_t initial_size, std::size_t max_size)
    : conn_str_(conn_str), max_size_(max_size) {
    if (initial_size > max_size_) initial_size = max_size_;
    for (std::size_t i = 0; i < initial_size; ++i) {
        try {
            auto conn = std::make_unique<pqxx::connection>(conn_str_);
            idle_.push_back(std::move(conn));
            ++total_;
        } catch (const std::exception& ex) {
            spdlog::warn("DB pool preconnect failed: {}", ex.what());
            break;
        }
    }
}

ConnectionPool::ConnPtr ConnectionPool::acquire() {
    std::unique_lock<std::mutex> lock(mtx_);
    while (idle_.empty()) {
        if (total_ < max_size_) {
            try {
                auto conn = std::make_unique<pqxx::connection>(conn_str_);
                ++total_;
                // release captured this
                return ConnPtr(conn.release(), [this](pqxx::connection* c) { release(c); });
            } catch (const std::exception& ex) {
                spdlog::error("DB pool connection create failed: {}", ex.what());
                // wait a bit for existing to free
                cv_.wait_for(lock, std::chrono::milliseconds(50));
                continue;
            }
        } else {
            cv_.wait(lock);
        }
    }
    auto conn = std::move(idle_.back());
    idle_.pop_back();
    return ConnPtr(conn.release(), [this](pqxx::connection* c) { release(c); });
}

void ConnectionPool::release(pqxx::connection* conn) {
    std::unique_lock<std::mutex> lock(mtx_);
    idle_.emplace_back(conn);
    lock.unlock();
    cv_.notify_one();
}
