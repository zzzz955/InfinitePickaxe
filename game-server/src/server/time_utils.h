#pragma once
#include <chrono>
#include <ctime>
#include <cstdint>

inline std::time_t timegm_compat(std::tm* tm) {
#if defined(_WIN32)
    return _mkgmtime(tm);
#else
    return timegm(tm);
#endif
}

inline uint64_t kst_next_midnight_ms() {
    using namespace std::chrono;
    auto now = system_clock::now();
    auto now_kst = now + hours(9);
    auto tt = system_clock::to_time_t(now_kst);
    std::tm tm = *std::gmtime(&tt);
    tm.tm_hour = 0;
    tm.tm_min = 0;
    tm.tm_sec = 0;
    tm.tm_mday += 1;
    std::time_t next_kst_tt = timegm_compat(&tm);
    auto next_midnight_utc = system_clock::from_time_t(next_kst_tt) - hours(9);
    return static_cast<uint64_t>(
        duration_cast<milliseconds>(next_midnight_utc.time_since_epoch()).count());
}
