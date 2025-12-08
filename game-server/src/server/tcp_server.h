#pragma once

#include <boost/asio.hpp>
#include <memory>
#include <vector>

class TcpServer {
public:
    TcpServer(boost::asio::io_context& io, unsigned short port);
    void start();

private:
    void do_accept();

    boost::asio::ip::tcp::acceptor acceptor_;
};
