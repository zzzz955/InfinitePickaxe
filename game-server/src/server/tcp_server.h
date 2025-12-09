#pragma once

#include <boost/asio.hpp>
#include "auth_service.h"
#include "game_repository.h"
#include "mining_service.h"
#include "upgrade_service.h"
#include "mission_service.h"
#include "slot_service.h"
#include "offline_service.h"
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
              OfflineService& offline_service);
    void start();

private:
    void do_accept();
    void start_handshake(std::shared_ptr<class Session> session);

    boost::asio::ip::tcp::acceptor acceptor_;
    AuthService& auth_service_;
    GameRepository& game_repo_;
    MiningService& mining_service_;
    UpgradeService& upgrade_service_;
    MissionService& mission_service_;
    SlotService& slot_service_;
    OfflineService& offline_service_;
};
