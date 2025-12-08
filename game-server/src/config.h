#pragma once
#include <string>
#include <cstdlib>

inline std::string env_or(const char* key, const char* def) {
    const char* v = std::getenv(key);
    return v ? std::string(v) : std::string(def);
}

struct ServerConfig {
    unsigned short listen_port = 10001;
    std::string auth_host = "auth-server";
    unsigned short auth_port = 10000;
    unsigned short health_port = 18080;
};

inline ServerConfig load_config() {
    ServerConfig cfg;
    cfg.listen_port = static_cast<unsigned short>(std::stoi(env_or("GAME_LISTEN_PORT", "10001")));
    cfg.auth_host = env_or("AUTH_HOST", "auth-server");
    cfg.auth_port = static_cast<unsigned short>(std::stoi(env_or("AUTH_PORT", "10000")));
    cfg.health_port = static_cast<unsigned short>(std::stoi(env_or("HEALTH_PORT", "18080")));
    return cfg;
}
