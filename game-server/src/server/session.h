#pragma once
#include <boost/asio.hpp>
#include <boost/asio/steady_timer.hpp>
#include <memory>
#include <string>
#include <chrono>
#include <array>
#include <limits>
#include <unordered_map>
#include "auth_service.h"
#include "game_repository.h"
#include "game.pb.h"
#include "message_router.h"
#include "mining_service.h"
#include "upgrade_service.h"
#include "mission_service.h"
#include "slot_service.h"
#include "offline_service.h"
#include "session_registry.h"

// 각 슬롯의 채굴 상태
struct SlotMiningState {
    uint32_t slot_index;          // 0-3
    uint64_t attack_power;        // 공격력
    float attack_speed;           // APS (attacks per second)
    uint32_t critical_hit_percent; // 크리티컬 확률 * 10000
    uint32_t critical_damage;      // 크리티컬 데미지 * 100
    float next_attack_timer_ms;   // 다음 공격까지 남은 시간 (밀리초)
};

// 세션의 채굴 상태
struct MiningState {
    bool is_mining = false;
    uint32_t current_mineral_id = 1;
    uint64_t current_hp = 0;
    uint64_t max_hp = 0;
    std::vector<SlotMiningState> slots;  // 활성화된 슬롯들
    float respawn_timer_ms = 0.0f;       // 리스폰 대기 중일 때 (5000ms)
    std::chrono::steady_clock::time_point last_update_time;
    uint64_t last_sent_hp = std::numeric_limits<uint64_t>::max(); // 마지막으로 전송한 HP (푸시 최소화)
};

class Session : public std::enable_shared_from_this<Session> {
public:
    Session(boost::asio::ip::tcp::socket socket,
            AuthService& auth_service,
            GameRepository& game_repo,
            MiningService& mining_service,
            UpgradeService& upgrade_service,
            MissionService& mission_service,
            SlotService& slot_service,
            OfflineService& offline_service,
            std::shared_ptr<SessionRegistry> registry,
            const class MetadataLoader& metadata);

    void start();
    void notify_duplicate_and_close();

    // 채굴 시뮬레이션 (40ms마다 TCPServer에서 호출)
    void update_mining_tick(float delta_ms = 40.0f);

private:
    void read_length();
    void read_payload(std::size_t length);
    void dispatch_envelope(const infinitepickaxe::Envelope& env);
    void handle_handshake(const infinitepickaxe::Envelope& env);
    void handle_heartbeat(const infinitepickaxe::Envelope& env);
    void handle_mining(const infinitepickaxe::Envelope& env);
    void handle_upgrade(const infinitepickaxe::Envelope& env);
    void handle_change_mineral(const infinitepickaxe::Envelope& env);
    void handle_mission(const infinitepickaxe::Envelope& env);
    void handle_mission_progress_update(const infinitepickaxe::Envelope& env);
    void handle_mission_complete(const infinitepickaxe::Envelope& env);
    void handle_mission_reroll(const infinitepickaxe::Envelope& env);
    void handle_ad_watch(const infinitepickaxe::Envelope& env);
    void handle_milestone_claim(const infinitepickaxe::Envelope& env);
    void handle_slot_unlock(const infinitepickaxe::Envelope& env);
    void handle_all_slots(const infinitepickaxe::Envelope& env);
    void handle_offline_reward(const infinitepickaxe::Envelope& env);
    void init_router();
    void send_envelope(const infinitepickaxe::Envelope& env);
    void send_error(const std::string& code, const std::string& message);
    bool is_expired() const;
    void start_auth_timer();
    void close();

    // 채굴 시뮬레이션 헬퍼 메서드
    void start_new_mineral();
    void send_mining_update(const std::vector<infinitepickaxe::PickaxeAttack>& attacks);
    void handle_mining_complete_immediate();
    void apply_slot_update(uint32_t slot_index, uint64_t attack_power, float attack_speed,
                           uint32_t critical_hit_percent, uint32_t critical_damage);
    void refresh_slots_from_service(bool preserve_timers);

    boost::asio::ip::tcp::socket socket_;
    boost::asio::steady_timer auth_timer_;
    AuthService& auth_service_;
    GameRepository& game_repo_;
    MessageRouter router_;
    MiningService& mining_service_;
    UpgradeService& upgrade_service_;
    MissionService& mission_service_;
    SlotService& slot_service_;
    OfflineService& offline_service_;
    std::shared_ptr<SessionRegistry> registry_;
    const class MetadataLoader& metadata_;

    // 세션 컨텍스트
    std::string user_id_;
    std::string device_id_;
    std::string google_id_;
    std::string client_ip_;
    std::chrono::system_clock::time_point expires_at_;
    bool authenticated_{false};
    bool closed_{false};
    uint32_t expected_seq_{1};
    uint32_t violation_count_{0};

    // 채굴 시뮬레이션 상태
    MiningState mining_state_;

    std::array<uint8_t, 4> len_buf_{};
    std::vector<uint8_t> payload_buf_;
};
