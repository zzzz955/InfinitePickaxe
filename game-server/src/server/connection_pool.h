#pragma once
#include <pqxx/pqxx>
#include <memory>
#include <string>
#include <mutex>
#include <condition_variable>
#include <functional>

// 단순 커넥션 풀: 초기 크기만큼 미리 연결, 부족하면 최대치까지 동적 확장
class ConnectionPool {
public:
    ConnectionPool(const std::string& conn_str, std::size_t initial_size, std::size_t max_size);

    using ConnPtr = std::unique_ptr<pqxx::connection, std::function<void(pqxx::connection*)>>;
    ConnPtr acquire();

private:
    void release(pqxx::connection* conn);

    std::string conn_str_;
    std::mutex mtx_;
    std::condition_variable cv_;
    std::vector<std::unique_ptr<pqxx::connection>> idle_;
    std::size_t max_size_;
    std::size_t total_{0};
};
