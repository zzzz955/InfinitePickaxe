#include "server/tcp_server.h"
#include "server/auth_service.h"
#include "server/game_repository.h"
#include "server/redis_client.h"
#include "server/connection_pool.h"
#include "config.h"
#include <boost/asio.hpp>
#include <spdlog/spdlog.h>
#include <thread>
#include <sstream>

int main() {
    try {
        ServerConfig cfg = load_config();
        DbConfig dbcfg{cfg.db_host, cfg.db_port, cfg.db_user, cfg.db_password, cfg.db_name};
        std::ostringstream conn_str;
        conn_str << "host=" << dbcfg.host
                 << " port=" << dbcfg.port
                 << " user=" << dbcfg.user
                 << " password=" << dbcfg.password
                 << " dbname=" << dbcfg.dbname;
        ConnectionPool db_pool(conn_str.str(), cfg.db_pool_size, cfg.db_pool_max);
        RedisClient redis_client(cfg.redis_host, cfg.redis_port);
        AuthService auth_service(cfg.auth_host, cfg.auth_port, redis_client);
        GameRepository game_repo(db_pool);
        boost::asio::io_context io;

        TcpServer server(io, cfg.listen_port, auth_service, game_repo);
        server.start();

        spdlog::info("Game server listening on port {}", cfg.listen_port);
        spdlog::info("Auth endpoint {}:{}", cfg.auth_host, cfg.auth_port);
        spdlog::info("DB endpoint {}:{} dbname={}", cfg.db_host, cfg.db_port, cfg.db_name);
        spdlog::info("Redis endpoint {}:{}", cfg.redis_host, cfg.redis_port);

        // 워커 스레드 풀 실행 (0이면 하드웨어 동시성)
        unsigned int workers = cfg.worker_threads;
        if (workers == 0) {
            workers = std::max(1u, std::thread::hardware_concurrency());
        }
        std::vector<std::thread> pool;
        for (unsigned int i = 0; i < workers; ++i) {
            pool.emplace_back([&io]() { io.run(); });
        }
        for (auto& t : pool) t.join();
    } catch (const std::exception& ex) {
        spdlog::error("Server crashed: {}", ex.what());
        return 1;
    }
    return 0;
}
