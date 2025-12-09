#pragma once
#include <string>
#include "http_auth_client.h"
#include "redis_client.h"

// 인증 + 세션 캐시를 담당하는 서비스
class AuthService {
public:
    AuthService(std::string auth_host, unsigned short auth_port, RedisClient& redis);

    // JWT 검증 + Redis 세션 기록 (성공 시)
    VerifyResult verify_and_cache(const std::string& jwt, const std::string& client_ip);

private:
    std::string auth_host_;
    unsigned short auth_port_;
    RedisClient& redis_;
};
