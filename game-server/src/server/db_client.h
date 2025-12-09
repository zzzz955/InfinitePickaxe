#pragma once
#include <string>

struct DbConfig {
    std::string host;
    unsigned short port;
    std::string user;
    std::string password;
    std::string dbname;
};

class DbClient {
public:
    explicit DbClient(DbConfig cfg);
    // 유저 기본 행과 슬롯 0번이 없으면 생성
    bool ensure_user_initialized(const std::string& user_id);

private:
    std::string conn_str_;
};
