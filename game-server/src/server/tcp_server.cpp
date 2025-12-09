#include "tcp_server.h"
#include "session.h"
#include <boost/asio.hpp>
#include <iostream>

TcpServer::TcpServer(boost::asio::io_context& io,
                     unsigned short port,
                     AuthService& auth_service,
                     GameRepository& game_repo)
    : acceptor_(io, boost::asio::ip::tcp::endpoint(boost::asio::ip::tcp::v4(), port)),
      auth_service_(auth_service),
      game_repo_(game_repo) {}

void TcpServer::start() {
    do_accept();
}

void TcpServer::do_accept() {
    acceptor_.async_accept(
        [this](boost::system::error_code ec, boost::asio::ip::tcp::socket socket) {
            if (!ec) {
                std::cout << "Accepted connection from " << socket.remote_endpoint() << std::endl;
                auto session = std::make_shared<Session>(std::move(socket), auth_service_, game_repo_);
                session->start();
            }
            do_accept();
        });
}
