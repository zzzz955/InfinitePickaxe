#include "session.h"
#include <iostream>

Session::Session(boost::asio::ip::tcp::socket socket,
                 AuthService& auth_service,
                 GameRepository& game_repo)
    : socket_(std::move(socket)),
      auth_service_(auth_service),
      game_repo_(game_repo) {}

void Session::start() {
    try {
        client_ip_ = socket_.remote_endpoint().address().to_string();
    } catch (...) {
        client_ip_.clear();
    }
    read_handshake();
}

void Session::read_handshake() {
    auto self = shared_from_this();
    boost::asio::async_read_until(socket_, buffer_, '\n',
        [this, self](boost::system::error_code ec, std::size_t /*len*/) {
            if (ec) {
                close();
                return;
            }
            std::istream is(&buffer_);
            std::string jwt;
            std::getline(is, jwt);
            if (!jwt.empty() && jwt.back() == '\r') jwt.pop_back();

            VerifyResult vr = auth_service_.verify_and_cache(jwt, client_ip_);
            if (!vr.valid || vr.is_banned) {
                std::string nack = "AUTH_FAIL\n";
                boost::asio::async_write(socket_, boost::asio::buffer(nack),
                    [this, self](boost::system::error_code /*ec*/, std::size_t /*written*/) {
                        close();
                    });
                return;
            }

            auto now = std::chrono::system_clock::now();
            if (vr.expires_at.time_since_epoch().count() != 0 && now >= vr.expires_at) {
                std::cout << "JWT expired, closing connection." << std::endl;
                close();
                return;
            }
            if (!game_repo_.ensure_user_initialized(vr.user_id)) {
                std::cout << "DB init failed, closing connection." << std::endl;
                close();
                return;
            }

            user_id_ = vr.user_id;
            device_id_ = vr.device_id;
            google_id_ = vr.google_id;
            expires_at_ = vr.expires_at;

            std::cout << "JWT verified for user " << (user_id_.empty() ? "unknown" : user_id_);
            if (!google_id_.empty()) std::cout << " (google_id=" << google_id_ << ")";
            if (!device_id_.empty()) std::cout << " device_id=" << device_id_;
            std::cout << ", session opened." << std::endl;

            std::string ack = "AUTH_OK\n";
            boost::asio::async_write(socket_, boost::asio::buffer(ack),
                [this, self](boost::system::error_code write_ec, std::size_t /*written*/) {
                    if (write_ec) {
                        close();
                        return;
                    }
                    start_message_loop();
                });
        });
}

void Session::start_message_loop() {
    auto self = shared_from_this();
    boost::asio::async_read_until(socket_, buffer_, '\n',
        [this, self](boost::system::error_code ec, std::size_t /*len*/) {
            if (ec) {
                close();
                return;
            }
            auto now = std::chrono::system_clock::now();
            if (expires_at_.time_since_epoch().count() != 0 && now >= expires_at_) {
                std::cout << "Session expired for user " << user_id_ << ", closing connection." << std::endl;
                close();
                return;
            }
            std::istream is(&buffer_);
            std::string msg;
            std::getline(is, msg);
            if (!msg.empty() && msg.back() == '\r') msg.pop_back();
            handle_message(msg);
        });
}

void Session::handle_message(const std::string& msg) {
    auto self = shared_from_this();
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
    boost::asio::async_write(socket_, boost::asio::buffer(resp),
        [this, self](boost::system::error_code write_ec, std::size_t /*written*/) {
            if (write_ec) {
                close();
                return;
            }
            start_message_loop();
        });
}

void Session::close() {
    boost::system::error_code ignored;
    socket_.shutdown(boost::asio::ip::tcp::socket::shutdown_both, ignored);
    socket_.close(ignored);
}
