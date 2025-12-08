#pragma once

#include <boost/asio.hpp>
#include "http_auth_client.h"
#include <memory>
#include <vector>
#include <string>
#include <functional>
#include <chrono>

class TcpServer {
public:
    using VerifyFn = std::function<VerifyResult(const std::string&)>;

    TcpServer(boost::asio::io_context& io, unsigned short port, VerifyFn verifier);
    void start();

private:
    void do_accept();
    void start_handshake(std::shared_ptr<boost::asio::ip::tcp::socket> socket);
    void start_echo(std::shared_ptr<boost::asio::ip::tcp::socket> socket,
                    std::chrono::system_clock::time_point expires_at);

    boost::asio::ip::tcp::acceptor acceptor_;
    VerifyFn verify_fn_;
};
