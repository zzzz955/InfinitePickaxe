#pragma once
#include <boost/asio.hpp>
#include <boost/asio/steady_timer.hpp>
#include <memory>
#include <string>
#include <chrono>
#include <array>
#include "auth_service.h"
#include "game_repository.h"
#include "game.pb.h"
#include "message_router.h"
#include "mining_service.h"
#include "upgrade_service.h"
#include "mission_service.h"
#include "slot_service.h"
#include "offline_service.h"
#include "session_registry.h"

class Session : public std::enable_shared_from_this<Session> {
public:
    Session(boost::asio::ip::tcp::socket socket,
            AuthService& auth_service,
            GameRepository& game_repo,
            MiningService& mining_service,
            UpgradeService& upgrade_service,
            MissionService& mission_service,
            SlotService& slot_service,
            OfflineService& offline_service,
            std::shared_ptr<SessionRegistry> registry);

    void start();
    void notify_duplicate_and_close();

private:
    void read_length();
    void read_payload(std::size_t length);
    void dispatch_envelope(const infinitepickaxe::Envelope& env);
    void handle_handshake(const infinitepickaxe::Envelope& env);
    void handle_heartbeat(const infinitepickaxe::Envelope& env);
    void handle_mining(const infinitepickaxe::Envelope& env);
    void handle_upgrade(const infinitepickaxe::Envelope& env);
    void handle_mission(const infinitepickaxe::Envelope& env);
    void handle_slot_unlock(const infinitepickaxe::Envelope& env);
    void handle_all_slots(const infinitepickaxe::Envelope& env);
    void handle_offline_reward(const infinitepickaxe::Envelope& env);
    void init_router();
    void send_envelope(const infinitepickaxe::Envelope& env);
    void send_error(const std::string& code, const std::string& message);
    bool is_expired() const;
    void start_auth_timer();
    void close();

    boost::asio::ip::tcp::socket socket_;
    boost::asio::steady_timer auth_timer_;
    AuthService& auth_service_;
    GameRepository& game_repo_;
    MessageRouter router_;
    MiningService& mining_service_;
    UpgradeService& upgrade_service_;
    MissionService& mission_service_;
    SlotService& slot_service_;
    OfflineService& offline_service_;
    std::shared_ptr<SessionRegistry> registry_;

    // 세션 컨텍스트
    std::string user_id_;
    std::string device_id_;
    std::string google_id_;
    std::string client_ip_;
    std::chrono::system_clock::time_point expires_at_;
    bool authenticated_{false};
    bool closed_{false};
    uint32_t expected_seq_{1};
    uint32_t violation_count_{0};

    std::array<uint8_t, 4> len_buf_{};
    std::vector<uint8_t> payload_buf_;
};
