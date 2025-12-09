#pragma once

#include <boost/asio.hpp>
#include "auth_service.h"
#include "game_repository.h"
#include <memory>
#include <vector>
#include <string>
#include <functional>
#include <chrono>

class TcpServer {
public:
    TcpServer(boost::asio::io_context& io,
              unsigned short port,
              AuthService& auth_service,
              GameRepository& game_repo);
    void start();

private:
    struct SessionCtx {
        std::string user_id;
        std::string device_id;
        std::chrono::system_clock::time_point expires_at;
    };

    void do_accept();
    void start_handshake(std::shared_ptr<boost::asio::ip::tcp::socket> socket);
    void start_message_loop(std::shared_ptr<boost::asio::ip::tcp::socket> socket, SessionCtx ctx);
    void handle_message(std::shared_ptr<boost::asio::ip::tcp::socket> socket,
                        SessionCtx& ctx,
                        const std::string& msg);

    boost::asio::ip::tcp::acceptor acceptor_;
    AuthService& auth_service_;
    GameRepository& game_repo_;
};
