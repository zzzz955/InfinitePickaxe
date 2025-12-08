#include "tcp_server.h"
#include <boost/asio.hpp>
#include <iostream>

TcpServer::TcpServer(boost::asio::io_context& io, unsigned short port, VerifyFn verifier)
    : acceptor_(io, boost::asio::ip::tcp::endpoint(boost::asio::ip::tcp::v4(), port)),
      verify_fn_(std::move(verifier)) {}

void TcpServer::start() {
    do_accept();
}

void TcpServer::do_accept() {
    auto socket = std::make_shared<boost::asio::ip::tcp::socket>(acceptor_.get_executor());
    acceptor_.async_accept(*socket, [this, socket](boost::system::error_code ec) {
        if (!ec) {
            std::cout << "Accepted connection from " << socket->remote_endpoint() << std::endl;
            start_handshake(socket);
        }
        do_accept();
    });
}

void TcpServer::start_handshake(std::shared_ptr<boost::asio::ip::tcp::socket> socket) {
    auto buffer = std::make_shared<boost::asio::streambuf>();
    boost::asio::async_read_until(*socket, *buffer, '\n',
        [this, socket, buffer](boost::system::error_code ec, std::size_t /*len*/) {
            if (ec) {
                std::cout << "Handshake read error: " << ec.message() << std::endl;
                socket->close();
                return;
            }
            std::istream is(buffer.get());
            std::string jwt;
            std::getline(is, jwt);
            if (!jwt.empty() && jwt.back() == '\r') jwt.pop_back();

            VerifyResult vr = verify_fn_ ? verify_fn_(jwt) : VerifyResult{};
            if (vr.valid) {
                auto now = std::chrono::system_clock::now();
                if (vr.expires_at.time_since_epoch().count() != 0 && now >= vr.expires_at) {
                    std::cout << "JWT expired, closing connection." << std::endl;
                    socket->close();
                    return;
                }
                std::cout << "JWT verified, session opened." << std::endl;
                start_echo(socket, vr.expires_at);
            } else {
                std::cout << "JWT invalid, closing connection." << std::endl;
                socket->close();
            }
        });
}

void TcpServer::start_echo(std::shared_ptr<boost::asio::ip::tcp::socket> socket,
                           std::chrono::system_clock::time_point expires_at) {
    auto buffer = std::make_shared<boost::asio::streambuf>();
    boost::asio::async_read_until(*socket, *buffer, '\n',
        [this, socket, buffer, expires_at](boost::system::error_code ec, std::size_t /*len*/) {
            if (ec) {
                socket->close();
                return;
            }
            auto now = std::chrono::system_clock::now();
            if (expires_at.time_since_epoch().count() != 0 && now >= expires_at) {
                std::cout << "Session expired, closing connection." << std::endl;
                socket->close();
                return;
            }
            std::istream is(buffer.get());
            std::string msg;
            std::getline(is, msg);
            if (!msg.empty() && msg.back() == '\r') msg.pop_back();
            std::cout << "Echo msg: " << msg << std::endl;
            // Echo back
            std::string echo_payload = msg + "\n";
            boost::asio::async_write(*socket, boost::asio::buffer(echo_payload),
                [this, socket, msg, expires_at](boost::system::error_code write_ec, std::size_t /*written*/) {
                    if (write_ec) {
                        socket->close();
                        return;
                    }
                    // Continue reading
                    start_echo(socket, expires_at);
                });
        });
}
