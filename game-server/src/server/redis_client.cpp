#include "redis_client.h"
#include <sw/redis++/redis++.h>
#include <spdlog/spdlog.h>

RedisClient::RedisClient(const std::string& host, unsigned short port)
    : host_(host), port_(port) {}

bool RedisClient::set_session(const std::string& user_id,
                              std::chrono::system_clock::time_point expires_at,
                              const std::string& device_id,
                              const std::string& client_ip) {
    try {
        sw::redis::ConnectionOptions opts;
        opts.host = host_;
        opts.port = static_cast<int>(port_);
        auto redis = sw::redis::Redis(opts);

        std::string key = "session:" + user_id;
        long long ttl_seconds = 300; // fallback 5분
        if (expires_at.time_since_epoch().count() != 0) {
            auto now = std::chrono::system_clock::now();
            auto diff = std::chrono::duration_cast<std::chrono::seconds>(expires_at - now).count();
            if (diff > 0) ttl_seconds = diff;
        }
        // 간단한 JSON 형태로 메타 저장
        std::string payload = std::string("{\"status\":\"AUTH_OK\"")
            + ",\"device_id\":\"" + device_id + "\""
            + ",\"client_ip\":\"" + client_ip + "\""
            + "}";

        redis.set(key, payload, std::chrono::seconds(ttl_seconds));
        return true;
    } catch (const std::exception& ex) {
        spdlog::warn("Redis set_session failed for user {}: {}", user_id, ex.what());
        return false;
    }
}
