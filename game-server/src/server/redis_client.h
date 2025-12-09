#pragma once
#include <string>
#include <chrono>

class RedisClient {
public:
    RedisClient(const std::string& host, unsigned short port);
    // 세션 키 기록 (유저 인증 성공 시), TTL은 만료 시각 기준 또는 fallback
    bool set_session(const std::string& user_id, std::chrono::system_clock::time_point expires_at);

private:
    std::string host_;
    unsigned short port_;
};
