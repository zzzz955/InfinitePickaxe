#include "tcp_server.h"
#include "session.h"
#include <boost/asio.hpp>
#include <iostream>

TcpServer::TcpServer(boost::asio::io_context& io,
                     unsigned short port,
                     AuthService& auth_service,
                     GameRepository& game_repo,
                     MiningService& mining_service,
                     UpgradeService& upgrade_service,
                     MissionService& mission_service,
                     SlotService& slot_service,
                     OfflineService& offline_service)
    : acceptor_(io, boost::asio::ip::tcp::endpoint(boost::asio::ip::tcp::v4(), port)),
      registry_(std::make_shared<SessionRegistry>()),
      auth_service_(auth_service),
      game_repo_(game_repo),
      mining_service_(mining_service),
      upgrade_service_(upgrade_service),
      mission_service_(mission_service),
      slot_service_(slot_service),
      offline_service_(offline_service) {}

void TcpServer::start() {
    do_accept();
}

void TcpServer::do_accept() {
    acceptor_.async_accept(
        [this](boost::system::error_code ec, boost::asio::ip::tcp::socket socket) {
            if (!ec) {
                std::cout << "Accepted connection from " << socket.remote_endpoint() << std::endl;
                auto session = std::make_shared<Session>(std::move(socket),
                                                         auth_service_,
                                                         game_repo_,
                                                         mining_service_,
                                                         upgrade_service_,
                                                         mission_service_,
                                                         slot_service_,
                                                         offline_service_,
                                                         registry_);
                session->start();
            }
            do_accept();
        });
}
