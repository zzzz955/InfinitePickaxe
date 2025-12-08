#pragma once
#include <string>

// Minimal HTTP auth client to call /auth/verify
bool verify_jwt_with_auth(const std::string& auth_host, unsigned short auth_port, const std::string& jwt);
