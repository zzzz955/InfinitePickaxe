#pragma once

#include <boost/asio.hpp>
#include "http_auth_client.h"
#include "db_client.h"
#include "redis_client.h"
#include <memory>
#include <vector>
#include <string>
#include <functional>
#include <chrono>

class TcpServer {
public:
    using VerifyFn = std::function<VerifyResult(const std::string&)>;
    using OnAuthFn = std::function<bool(const VerifyResult&)>;

    TcpServer(boost::asio::io_context& io, unsigned short port, VerifyFn verifier, OnAuthFn on_auth);
    void start();

private:
    void do_accept();
    void start_handshake(std::shared_ptr<boost::asio::ip::tcp::socket> socket);

    boost::asio::ip::tcp::acceptor acceptor_;
    VerifyFn verify_fn_;
    OnAuthFn on_auth_fn_;
};
