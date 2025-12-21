#pragma once
#include <string>
#include <chrono>
#include <optional>
#include <unordered_map>

class RedisClient {
public:
    RedisClient(const std::string& host, unsigned short port);
    // 세션 키 기록 (유저 인증 성공 시), TTL은 만료 시각 기준 또는 fallback
    bool set_session(const std::string& user_id,
                     std::chrono::system_clock::time_point expires_at,
                     const std::string& device_id,
                     const std::string& client_ip);

    bool hset_fields(const std::string& key,
                     const std::unordered_map<std::string, std::string>& fields,
                     std::chrono::seconds ttl);
    bool hgetall(const std::string& key,
                 std::unordered_map<std::string, std::string>& out_fields);
    bool set_string(const std::string& key, const std::string& value, std::chrono::seconds ttl);
    std::optional<std::string> get_string(const std::string& key);

private:
    std::string host_;
    unsigned short port_;
};
