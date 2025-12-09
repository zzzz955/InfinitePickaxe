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
    void do_accept();
    void start_handshake(std::shared_ptr<class Session> session);

    boost::asio::ip::tcp::acceptor acceptor_;
    AuthService& auth_service_;
    GameRepository& game_repo_;
};
