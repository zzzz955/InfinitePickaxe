#pragma once
#include <boost/asio.hpp>
#include <memory>
#include <string>
#include <chrono>
#include <array>
#include "auth_service.h"
#include "game_repository.h"
#include "game.pb.h"

class Session : public std::enable_shared_from_this<Session> {
public:
    Session(boost::asio::ip::tcp::socket socket,
            AuthService& auth_service,
            GameRepository& game_repo);

    void start();

private:
    void read_length();
    void read_payload(std::size_t length);
    void dispatch_envelope(const infinitepickaxe::Envelope& env);
    void handle_handshake(const infinitepickaxe::Envelope& env);
    void handle_heartbeat(const infinitepickaxe::Envelope& env);
    void handle_mining(const infinitepickaxe::Envelope& env, const std::string& type);
    void handle_upgrade(const infinitepickaxe::Envelope& env);
    void handle_mission(const infinitepickaxe::Envelope& env, const std::string& type);
    void handle_slot_unlock(const infinitepickaxe::Envelope& env);
    void handle_offline_reward(const infinitepickaxe::Envelope& env);
    void send_envelope(const std::string& msg_type, const google::protobuf::Message& msg);
    void send_error(const std::string& code, const std::string& message);
    bool is_expired() const;
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
    bool authenticated_{false};
    uint32_t expected_seq_{1};
    uint32_t violation_count_{0};

    std::array<uint8_t, 4> len_buf_{};
    std::vector<uint8_t> payload_buf_;
};
