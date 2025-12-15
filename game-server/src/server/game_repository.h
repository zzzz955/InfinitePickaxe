#pragma once
#include <string>
#include <vector>
#include <optional>

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
    std::vector<bool> unlocked_slots;     // 4개
    uint64_t total_dps;                   // 모든 슬롯의 DPS 합계 (캐시)
    std::optional<uint32_t> current_mineral_id;  // 현재 채굴 중인 광물 ID (nullable)
    std::optional<uint64_t> current_mineral_hp;  // 현재 광물 HP (nullable)
    // ad_count_today 제거 → user_ad_counters 테이블로 이동
    // mission_rerolls_used 제거 → user_mission_daily 테이블로 이동
};

// 게임 데이터 접근을 담당하는 리포지토리
class GameRepository {
public:
    explicit GameRepository(class ConnectionPool& pool);

    // 유저 기본 행, 슬롯 0번을 없으면 생성
    bool ensure_user_initialized(const std::string& user_id);

    // 유저 게임 데이터 조회
    UserGameData get_user_game_data(const std::string& user_id);
    std::optional<uint32_t> add_crystal(const std::string& user_id, uint32_t delta);
    bool set_current_mineral(const std::string& user_id, uint32_t mineral_id, uint64_t mineral_hp);

private:
    class ConnectionPool& pool_;
};
