#include "http_auth_client.h"
#include <httplib.h>
#include <string>

bool verify_jwt_with_auth(const std::string& auth_host, unsigned short auth_port, const std::string& jwt) {
    if (jwt.empty()) return false;
    httplib::Client cli(auth_host, static_cast<int>(auth_port));
    cli.set_connection_timeout(2, 0);
    cli.set_read_timeout(3, 0);
    cli.set_write_timeout(3, 0);

    std::string body = std::string("{\"jwt\":\"") + jwt + "\"}";
    auto res = cli.Post("/auth/verify", body, "application/json");
    if (!res || res->status != 200) return false;
    // Very lightweight check: look for "valid":true
    return res->body.find("\"valid\":true") != std::string::npos;
}
