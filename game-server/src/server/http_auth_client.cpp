#include "http_auth_client.h"
#include <httplib.h>
#include <string>
#include <cctype>
#include <cstdlib>

namespace {
std::string parse_string_field(const std::string& body, const std::string& key) {
    const std::string pattern = "\"" + key + "\":\"";
    auto pos = body.find(pattern);
    if (pos == std::string::npos) return {};
    pos += pattern.size();
    auto end = body.find('"', pos);
    if (end == std::string::npos) return {};
    return body.substr(pos, end - pos);
}
} // namespace

VerifyResult verify_jwt_with_auth(const std::string& auth_host, unsigned short auth_port, const std::string& jwt) {
    VerifyResult result{};
    if (jwt.empty()) return result;
    httplib::Client cli(auth_host, static_cast<int>(auth_port));
    cli.set_connection_timeout(2, 0);
    cli.set_read_timeout(3, 0);
    cli.set_write_timeout(3, 0);

    std::string body = std::string("{\"jwt\":\"") + jwt + "\"}";
    auto res = cli.Post("/auth/verify", body, "application/json");
    if (!res || res->status != 200) return result;

    // Minimal parse: check validity flag
    if (res->body.find("\"valid\":true") == std::string::npos) return result;
    result.valid = true;
    result.user_id = parse_string_field(res->body, "user_id");
    result.google_id = parse_string_field(res->body, "google_id");
    result.device_id = parse_string_field(res->body, "device_id");

    // Extract expires_at epoch seconds if present
    const std::string key = "\"expires_at\":";
    auto pos = res->body.find(key);
    if (pos != std::string::npos) {
        pos += key.size();
        while (pos < res->body.size() && std::isspace(static_cast<unsigned char>(res->body[pos]))) {
            ++pos;
        }
        size_t start = pos;
        while (pos < res->body.size() && std::isdigit(static_cast<unsigned char>(res->body[pos]))) {
            ++pos;
        }
        if (pos > start) {
            try {
                auto exp = std::stoll(res->body.substr(start, pos - start));
                result.expires_at = std::chrono::system_clock::time_point{std::chrono::seconds{exp}};
            } catch (...) {
                // ignore parse errors; caller will treat empty time_point as unknown
            }
        }
    }

    // banned flag and reason (if provided)
    if (res->body.find("\"is_banned\":true") != std::string::npos) {
        result.is_banned = true;
    }
    result.ban_reason = parse_string_field(res->body, "ban_reason");

    return result;
}
