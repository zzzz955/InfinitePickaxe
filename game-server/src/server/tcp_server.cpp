#include "tcp_server.h"
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

            std::string client_ip;
            try {
                client_ip = socket->remote_endpoint().address().to_string();
            } catch (...) {
                client_ip = "";
            }

            VerifyResult vr = auth_service_.verify_and_cache(jwt, client_ip);
            if (vr.valid && !vr.is_banned) {
                auto now = std::chrono::system_clock::now();
                if (vr.expires_at.time_since_epoch().count() != 0 && now >= vr.expires_at) {
                    std::cout << "JWT expired, closing connection." << std::endl;
                    socket->close();
                    return;
                }

                if (!game_repo_.ensure_user_initialized(vr.user_id)) {
                    std::cout << "Auth DB init failed, closing connection." << std::endl;
                    socket->close();
                    return;
                }
                std::cout << "JWT verified for user " << (vr.user_id.empty() ? "unknown" : vr.user_id);
                if (!vr.google_id.empty()) {
                    std::cout << " (google_id=" << vr.google_id << ")";
                }
                if (!vr.device_id.empty()) {
                    std::cout << " device_id=" << vr.device_id;
                }
                std::cout << ", session opened." << std::endl;
                SessionCtx ctx{vr.user_id, vr.device_id, vr.expires_at};
                std::string ack = "AUTH_OK\n";
                boost::asio::async_write(*socket, boost::asio::buffer(ack),
                    [this, socket, ctx](boost::system::error_code write_ec, std::size_t /*written*/) mutable {
                        if (write_ec) {
                            socket->close();
                            return;
                        }
                        start_message_loop(socket, ctx);
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

void TcpServer::start_message_loop(std::shared_ptr<boost::asio::ip::tcp::socket> socket, SessionCtx ctx) {
    auto buffer = std::make_shared<boost::asio::streambuf>();
    boost::asio::async_read_until(*socket, *buffer, '\n',
        [this, socket, buffer, ctx](boost::system::error_code ec, std::size_t /*len*/) mutable {
            if (ec) {
                socket->close();
                return;
            }
            auto now = std::chrono::system_clock::now();
            if (ctx.expires_at.time_since_epoch().count() != 0 && now >= ctx.expires_at) {
                std::cout << "Session expired for user " << ctx.user_id << ", closing connection." << std::endl;
                socket->close();
                return;
            }
            std::istream is(buffer.get());
            std::string msg;
            std::getline(is, msg);
            if (!msg.empty() && msg.back() == '\r') msg.pop_back();
            handle_message(socket, ctx, msg);
        });
}

void TcpServer::handle_message(std::shared_ptr<boost::asio::ip::tcp::socket> socket,
                               SessionCtx& ctx,
                               const std::string& msg) {
    // 매우 단순한 프로토콜 스텁: 한 줄 문자열 기반
    std::string resp;
    if (msg == "PING") {
        resp = "PONG\n";
    } else if (msg == "MINE_START") {
        resp = "ACK:MINE_START\n";
    } else if (msg == "MINE_PROGRESS") {
        resp = "ACK:MINE_PROGRESS\n";
    } else if (msg == "MINE_COMPLETE") {
        resp = "ACK:MINE_COMPLETE\n";
    } else if (msg == "MISSION_UPDATE") {
        resp = "ACK:MISSION_UPDATE\n";
    } else {
        resp = "ERR:UNKNOWN\n";
    }
    boost::asio::async_write(*socket, boost::asio::buffer(resp),
        [this, socket, ctx](boost::system::error_code write_ec, std::size_t /*written*/) mutable {
            if (write_ec) {
                socket->close();
                return;
            }
            start_message_loop(socket, ctx);
        });
}
