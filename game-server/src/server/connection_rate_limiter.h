#pragma once
#include <chrono>
#include <string>
#include <unordered_map>
#include <mutex>

// 간단한 per-IP 연결 레이트 리미터
class ConnectionRateLimiter {
public:
    ConnectionRateLimiter(std::size_t max_connections_per_window,
                          std::chrono::seconds window)
        : max_connections_per_window_(max_connections_per_window),
          window_(window) {}

    bool allow(const std::string& ip);

private:
    std::size_t max_connections_per_window_;
    std::chrono::seconds window_;
    struct Counter {
        std::size_t count{0};
        std::chrono::steady_clock::time_point window_start{};
    };
    std::unordered_map<std::string, Counter> counters_;
    std::mutex mutex_;
};
