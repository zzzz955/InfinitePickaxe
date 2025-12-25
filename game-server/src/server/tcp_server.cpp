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
                     OfflineService& offline_service,
                     AdService& ad_service,
                     GemService& gem_service,
                     RedisClient& redis_client,
                     const MetadataLoader& metadata)
    : acceptor_(io, boost::asio::ip::tcp::endpoint(boost::asio::ip::tcp::v4(), port)),
      mining_tick_timer_(io),
      registry_(std::make_shared<SessionRegistry>()),
      auth_service_(auth_service),
      game_repo_(game_repo),
      mining_service_(mining_service),
      upgrade_service_(upgrade_service),
      mission_service_(mission_service),
      slot_service_(slot_service),
      offline_service_(offline_service),
      ad_service_(ad_service),
      gem_service_(gem_service),
      redis_client_(redis_client),
      metadata_(metadata) {
    rate_limiter_ = std::make_shared<ConnectionRateLimiter>(10, std::chrono::seconds(10));
}

void TcpServer::start() {
    do_accept();
    start_mining_tick();  // 40ms 채굴 틱 시작
}

void TcpServer::do_accept() {
    acceptor_.async_accept(
        [this](boost::system::error_code ec, boost::asio::ip::tcp::socket socket) {
            if (!ec) {
                std::string ip;
                try {
                    ip = socket.remote_endpoint().address().to_string();
                } catch (...) {
                    ip.clear();
                }

                if (!ip.empty() && rate_limiter_ && !rate_limiter_->allow(ip)) {
                    std::cout << "Connection rate limit exceeded for " << ip << std::endl;
                    boost::system::error_code ignored;
                    socket.shutdown(boost::asio::ip::tcp::socket::shutdown_both, ignored);
                    socket.close(ignored);
                } else {
                    // TCP_NODELAY 설정 (Nagle 알고리즘 비활성화 - 40ms 틱 즉시 전송)
                    boost::asio::ip::tcp::no_delay option(true);
                    boost::system::error_code ec_nodelay;
                    socket.set_option(option, ec_nodelay);
                    if (ec_nodelay) {
                        std::cerr << "Failed to set TCP_NODELAY: " << ec_nodelay.message() << std::endl;
                    }

                    std::cout << "Accepted connection from " << socket.remote_endpoint() << std::endl;
                    auto session = std::make_shared<Session>(std::move(socket),
                                                             auth_service_,
                                                             game_repo_,
                                                             mining_service_,
                                                            upgrade_service_,
                                                            mission_service_,
                                                            slot_service_,
                                                            offline_service_,
                                                            ad_service_,
                                                            gem_service_,
                                                            redis_client_,
                                                            registry_,
                                                            metadata_);
                    session->start();
                }
            }
            do_accept();
        });
}

void TcpServer::start_mining_tick() {
    // 40ms 후에 실행되도록 타이머 설정
    mining_tick_timer_.expires_after(std::chrono::milliseconds(40));

    mining_tick_timer_.async_wait([this](boost::system::error_code ec) {
        if (!ec) {
            // 모든 활성 세션의 채굴 시뮬레이션 업데이트
            auto sessions = registry_->get_all_sessions();
            for (auto& session : sessions) {
                session->update_mining_tick(40.0f);  // 40ms
            }

            // 다음 틱 스케줄링 (재귀)
            start_mining_tick();
        }
    });
}
