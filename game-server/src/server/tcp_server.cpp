#include "tcp_server.h"
#include <boost/asio.hpp>
#include <iostream>

TcpServer::TcpServer(boost::asio::io_context& io, unsigned short port, VerifyFn verifier, OnAuthFn on_auth)
    : acceptor_(io, boost::asio::ip::tcp::endpoint(boost::asio::ip::tcp::v4(), port)),
      verify_fn_(std::move(verifier)),
      on_auth_fn_(std::move(on_auth)) {}

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
            if (vr.valid && !vr.is_banned) {
                auto now = std::chrono::system_clock::now();
                if (vr.expires_at.time_since_epoch().count() != 0 && now >= vr.expires_at) {
                    std::cout << "JWT expired, closing connection." << std::endl;
                    socket->close();
                    return;
                }
                if (on_auth_fn_) {
                    if (!on_auth_fn_(vr)) {
                        std::cout << "Auth DB init failed, closing connection." << std::endl;
                        socket->close();
                        return;
                    }
                }
                std::cout << "JWT verified for user " << (vr.user_id.empty() ? "unknown" : vr.user_id);
                if (!vr.google_id.empty()) {
                    std::cout << " (google_id=" << vr.google_id << ")";
                }
                if (!vr.device_id.empty()) {
                    std::cout << " device_id=" << vr.device_id;
                }
                std::cout << ", session opened." << std::endl;
                // Send minimal ACK and close (no echo loop in MVP stub)
                std::string ack = "AUTH_OK\n";
                boost::asio::async_write(*socket, boost::asio::buffer(ack),
                    [socket](boost::system::error_code write_ec, std::size_t /*written*/) {
                        socket->shutdown(boost::asio::ip::tcp::socket::shutdown_both);
                        socket->close();
                    });
            } else {
                if (vr.is_banned) {
                    std::cout << "User banned";
                    if (!vr.ban_reason.empty()) std::cout << ": " << vr.ban_reason;
                    std::cout << ", closing connection." << std::endl;
                } else {
                    std::cout << "JWT invalid, closing connection." << std::endl;
                }
                std::string nack = "AUTH_FAIL\n";
                boost::asio::async_write(*socket, boost::asio::buffer(nack),
                    [socket](boost::system::error_code /*ec*/, std::size_t /*written*/) {
                        socket->shutdown(boost::asio::ip::tcp::socket::shutdown_both);
                        socket->close();
                    });
            }
        });
}
