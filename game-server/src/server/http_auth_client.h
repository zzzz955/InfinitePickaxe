#pragma once
#include <string>
#include <chrono>

struct VerifyResult {
    bool valid{false};
    std::chrono::system_clock::time_point expires_at{};
};

// Minimal HTTP auth client to call /auth/verify
VerifyResult verify_jwt_with_auth(const std::string& auth_host, unsigned short auth_port, const std::string& jwt);
