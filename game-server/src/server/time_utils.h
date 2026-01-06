#pragma once
#include <chrono>
#include <ctime>
#include <cstdint>
#include <string>

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

inline std::string kst_week_start_date_string() {
    using namespace std::chrono;
    auto now = system_clock::now();
    auto now_kst = now + hours(9);
    auto tt = system_clock::to_time_t(now_kst);
    std::tm tm = *std::gmtime(&tt);
    int days_since_monday = (tm.tm_wday + 6) % 7;
    tm.tm_hour = 0;
    tm.tm_min = 0;
    tm.tm_sec = 0;
    tm.tm_mday -= days_since_monday;
    char buf[11];
    std::strftime(buf, sizeof(buf), "%Y-%m-%d", &tm);
    return std::string(buf);
}

inline uint64_t kst_next_week_reset_ms() {
    using namespace std::chrono;
    auto now = system_clock::now();
    auto now_kst = now + hours(9);
    auto tt = system_clock::to_time_t(now_kst);
    std::tm tm = *std::gmtime(&tt);
    int days_since_monday = (tm.tm_wday + 6) % 7;
    tm.tm_hour = 0;
    tm.tm_min = 0;
    tm.tm_sec = 0;
    tm.tm_mday -= days_since_monday;
    std::time_t monday_kst_tt = timegm_compat(&tm);
    auto next_monday_utc = system_clock::from_time_t(monday_kst_tt) + hours(24 * 7) - hours(9);
    return static_cast<uint64_t>(
        duration_cast<milliseconds>(next_monday_utc.time_since_epoch()).count());
}
