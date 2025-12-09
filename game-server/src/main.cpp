#include "server/tcp_server.h"
#include "server/http_auth_client.h"
#include "server/db_client.h"
#include "server/redis_client.h"
#include "config.h"
#include <boost/asio.hpp>
#include <spdlog/spdlog.h>
#include <thread>

int main() {
    try {
        ServerConfig cfg = load_config();
        DbConfig dbcfg{cfg.db_host, cfg.db_port, cfg.db_user, cfg.db_password, cfg.db_name};
        DbClient db_client(dbcfg);
        RedisClient redis_client(cfg.redis_host, cfg.redis_port);
        boost::asio::io_context io;

        // JWT verifier using auth-service HTTP
        auto verifier = [cfg](const std::string& jwt) {
            return verify_jwt_with_auth(cfg.auth_host, cfg.auth_port, jwt);
        };

        // On-auth hook: ensure DB rows exist
        auto on_auth = [&db_client, &redis_client](const VerifyResult& vr) {
            if (vr.user_id.empty()) return false;
            if (!db_client.ensure_user_initialized(vr.user_id)) {
                return false;
            }
            redis_client.set_session(vr.user_id, vr.expires_at, vr.device_id, "");
            return true;
        };

        TcpServer server(io, cfg.listen_port, verifier, on_auth);
        server.start();

        spdlog::info("Game server listening on port {}", cfg.listen_port);
        spdlog::info("Auth endpoint {}:{}", cfg.auth_host, cfg.auth_port);
        spdlog::info("DB endpoint {}:{} dbname={}", cfg.db_host, cfg.db_port, cfg.db_name);
        spdlog::info("Redis endpoint {}:{}", cfg.redis_host, cfg.redis_port);

        io.run();
    } catch (const std::exception& ex) {
        spdlog::error("Server crashed: {}", ex.what());
        return 1;
    }
    return 0;
}
