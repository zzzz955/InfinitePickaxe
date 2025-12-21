#pragma once

#include <boost/asio.hpp>
#include "auth_service.h"
#include "game_repository.h"
#include "mining_service.h"
#include "upgrade_service.h"
#include "mission_service.h"
#include "slot_service.h"
#include "offline_service.h"
#include "session_registry.h"
#include "connection_rate_limiter.h"
#include "redis_client.h"
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
              GameRepository& game_repo,
              MiningService& mining_service,
              UpgradeService& upgrade_service,
              MissionService& mission_service,
              SlotService& slot_service,
              OfflineService& offline_service,
              RedisClient& redis_client,
              const class MetadataLoader& metadata);
    void start();

private:
    void do_accept();
    void start_mining_tick();  // 40ms 채굴 틱 시작

    boost::asio::ip::tcp::acceptor acceptor_;
    boost::asio::steady_timer mining_tick_timer_;  // 40ms 타이머
    std::shared_ptr<SessionRegistry> registry_;
    std::shared_ptr<ConnectionRateLimiter> rate_limiter_;
    AuthService& auth_service_;
    GameRepository& game_repo_;
    MiningService& mining_service_;
    UpgradeService& upgrade_service_;
    MissionService& mission_service_;
    SlotService& slot_service_;
    OfflineService& offline_service_;
    RedisClient& redis_client_;
    const class MetadataLoader& metadata_;
};
