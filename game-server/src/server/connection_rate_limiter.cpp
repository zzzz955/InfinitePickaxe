#include "connection_rate_limiter.h"

bool ConnectionRateLimiter::allow(const std::string& ip) {
    if (ip.empty()) return true;
    std::lock_guard<std::mutex> lock(mutex_);
    auto now = std::chrono::steady_clock::now();
    auto& ctr = counters_[ip];
    if (ctr.window_start.time_since_epoch().count() == 0 ||
        now - ctr.window_start >= window_) {
        ctr.window_start = now;
        ctr.count = 0;
    }
    if (ctr.count >= max_connections_per_window_) {
        return false;
    }
    ++ctr.count;
    return true;
}
