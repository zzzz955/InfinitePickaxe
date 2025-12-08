#include "server/tcp_server.h"
#include <boost/asio.hpp>
#include <spdlog/spdlog.h>

int main() {
    try {
        boost::asio::io_context io;
        const unsigned short port = 10001; // per spec

        TcpServer server(io, port);
        server.start();

        spdlog::info("Game server listening on port {}", port);
        io.run();
    } catch (const std::exception& ex) {
        spdlog::error("Server crashed: {}", ex.what());
        return 1;
    }
    return 0;
}
