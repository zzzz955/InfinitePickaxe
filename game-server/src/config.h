#pragma once
#include <string>
#include <cstdlib>

inline std::string env_or(const char* key, const char* def) {
    const char* v = std::getenv(key);
    return v ? std::string(v) : std::string(def);
}

inline unsigned int parse_uint_or(const char* key, const char* def) {
    std::string raw = env_or(key, def);
    try {
        return static_cast<unsigned int>(std::stoul(raw));
    } catch (...) {
        return static_cast<unsigned int>(std::stoul(def));
    }
}

inline unsigned short parse_ushort_or(const char* key, const char* def) {
    std::string raw = env_or(key, def);
    try {
        return static_cast<unsigned short>(std::stoul(raw));
    } catch (...) {
        return static_cast<unsigned short>(std::stoul(def));
    }
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
    // DB 커넥션 풀
    unsigned int db_pool_size = 4;
    unsigned int db_pool_max = 16;
    // Redis 접속 설정
    std::string redis_host = "redis";
    unsigned short redis_port = 6379;
    // 워커 스레드 수 (0이면 하드웨어 동시성)
    unsigned int worker_threads = 0;
};

inline ServerConfig load_config() {
    ServerConfig cfg;
    cfg.listen_port = parse_ushort_or("GAME_LISTEN_PORT", "10001");
    cfg.auth_host = env_or("AUTH_HOST", "auth-server");
    cfg.auth_port = parse_ushort_or("AUTH_PORT", "10000");
    cfg.health_port = parse_ushort_or("HEALTH_PORT", "18080");
    cfg.db_host = env_or("DB_HOST", "postgres");
    cfg.db_port = parse_ushort_or("DB_PORT", "5432");
    cfg.db_user = env_or("DB_USER", "pickaxe");
    cfg.db_password = env_or("DB_PASSWORD", "pickaxe");
    cfg.db_name = env_or("DB_NAME", "pickaxe_auth");
    cfg.db_pool_size = parse_uint_or("DB_POOL_SIZE", "4");
    cfg.db_pool_max = parse_uint_or("DB_POOL_MAX", "16");
    cfg.redis_host = env_or("REDIS_HOST", "redis");
    cfg.redis_port = parse_ushort_or("REDIS_PORT", "6379");
    cfg.worker_threads = parse_uint_or("WORKER_THREADS", "0");
    return cfg;
}
