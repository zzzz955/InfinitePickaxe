#pragma once
#include <string>
#include <vector>

struct DbConfig {
    std::string host;
    unsigned short port;
    std::string user;
    std::string password;
    std::string dbname;
};

struct UserGameData {
    uint64_t gold;
    uint32_t crystal;
    std::vector<bool> unlocked_slots;  // 4개
    uint32_t current_mineral_id;       // 현재 채굴 중인 광물 ID
    uint64_t current_mineral_hp;       // 현재 광물 HP
    uint32_t ad_count_today;
    uint32_t mission_rerolls_used;  // mission_reroll_free + mission_reroll_ad의 사용량
    uint32_t max_offline_hours;
};

// 게임 데이터 접근을 담당하는 리포지토리
class GameRepository {
public:
    explicit GameRepository(class ConnectionPool& pool);

    // 유저 기본 행, 슬롯 0번을 없으면 생성
    bool ensure_user_initialized(const std::string& user_id);

    // 유저 게임 데이터 조회
    UserGameData get_user_game_data(const std::string& user_id);

private:
    class ConnectionPool& pool_;
};
