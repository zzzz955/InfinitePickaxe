#pragma once

#include <boost/asio.hpp>
#include <memory>
#include <vector>
#include <string>
#include <functional>

class TcpServer {
public:
    using VerifyFn = std::function<bool(const std::string&)>;

    TcpServer(boost::asio::io_context& io, unsigned short port, VerifyFn verifier);
    void start();

private:
    void do_accept();
    void start_handshake(std::shared_ptr<boost::asio::ip::tcp::socket> socket);
    void start_echo(std::shared_ptr<boost::asio::ip::tcp::socket> socket);

    boost::asio::ip::tcp::acceptor acceptor_;
    VerifyFn verify_fn_;
};
