#pragma once
#include <boost/asio.hpp>
#include <memory>
#include <string>
#include <chrono>
#include "auth_service.h"
#include "game_repository.h"

class Session : public std::enable_shared_from_this<Session> {
public:
    Session(boost::asio::ip::tcp::socket socket,
            AuthService& auth_service,
            GameRepository& game_repo);

    void start();

private:
    void read_handshake();
    void start_message_loop();
    void handle_message(const std::string& msg);
    void close();

    boost::asio::ip::tcp::socket socket_;
    AuthService& auth_service_;
    GameRepository& game_repo_;

    // 세션 컨텍스트
    std::string user_id_;
    std::string device_id_;
    std::string google_id_;
    std::string client_ip_;
    std::chrono::system_clock::time_point expires_at_;

    boost::asio::streambuf buffer_;
};
