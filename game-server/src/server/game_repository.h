#pragma once
#include <string>

struct DbConfig {
    std::string host;
    unsigned short port;
    std::string user;
    std::string password;
    std::string dbname;
};

// 게임 데이터 접근을 담당하는 리포지토리
class GameRepository {
public:
    explicit GameRepository(class ConnectionPool& pool);

    // 유저 기본 행, 슬롯 0번을 없으면 생성
    bool ensure_user_initialized(const std::string& user_id);

private:
    class ConnectionPool& pool_;
};
