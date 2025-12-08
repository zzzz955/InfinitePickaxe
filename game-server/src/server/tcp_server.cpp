#include "tcp_server.h"
#include <iostream>

TcpServer::TcpServer(boost::asio::io_context& io, unsigned short port)
    : acceptor_(io, boost::asio::ip::tcp::endpoint(boost::asio::ip::tcp::v4(), port)) {}

void TcpServer::start() {
    do_accept();
}

void TcpServer::do_accept() {
    auto socket = std::make_shared<boost::asio::ip::tcp::socket>(acceptor_.get_executor());
    acceptor_.async_accept(*socket, [this, socket](boost::system::error_code ec) {
        if (!ec) {
            std::cout << "Accepted connection from " << socket->remote_endpoint() << std::endl;
        }
        do_accept();
    });
}
