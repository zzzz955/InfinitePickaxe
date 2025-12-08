#include "server/tcp_server.h"
#include "server/http_auth_client.h"
#include "config.h"
#include <boost/asio.hpp>
#include <spdlog/spdlog.h>
#include <thread>

int main() {
    try {
        ServerConfig cfg = load_config();
        boost::asio::io_context io;

        // JWT verifier using auth-service HTTP
        auto verifier = [cfg](const std::string& jwt) {
            return verify_jwt_with_auth(cfg.auth_host, cfg.auth_port, jwt);
        };

        TcpServer server(io, cfg.listen_port, verifier);
        server.start();

        spdlog::info("Game server listening on port {}", cfg.listen_port);
        spdlog::info("Auth endpoint {}:{}", cfg.auth_host, cfg.auth_port);

        io.run();
    } catch (const std::exception& ex) {
        spdlog::error("Server crashed: {}", ex.what());
        return 1;
    }
    return 0;
}
