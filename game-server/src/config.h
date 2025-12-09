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
    // DB 접속 설정
    std::string db_host = "postgres";
    unsigned short db_port = 5432;
    std::string db_user = "pickaxe";
    std::string db_password = "pickaxe";
    std::string db_name = "pickaxe_auth";
    // Redis 접속 설정
    std::string redis_host = "redis";
    unsigned short redis_port = 6379;
};

inline ServerConfig load_config() {
    ServerConfig cfg;
    cfg.listen_port = static_cast<unsigned short>(std::stoi(env_or("GAME_LISTEN_PORT", "10001")));
    cfg.auth_host = env_or("AUTH_HOST", "auth-server");
    cfg.auth_port = static_cast<unsigned short>(std::stoi(env_or("AUTH_PORT", "10000")));
    cfg.health_port = static_cast<unsigned short>(std::stoi(env_or("HEALTH_PORT", "18080")));
    cfg.db_host = env_or("DB_HOST", "postgres");
    cfg.db_port = static_cast<unsigned short>(std::stoi(env_or("DB_PORT", "5432")));
    cfg.db_user = env_or("DB_USER", "pickaxe");
    cfg.db_password = env_or("DB_PASSWORD", "pickaxe");
    cfg.db_name = env_or("DB_NAME", "pickaxe_auth");
    cfg.redis_host = env_or("REDIS_HOST", "redis");
    cfg.redis_port = static_cast<unsigned short>(std::stoi(env_or("REDIS_PORT", "6379")));
    return cfg;
}
