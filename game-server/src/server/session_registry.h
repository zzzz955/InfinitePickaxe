#pragma once
#include <memory>
#include <string>
#include <unordered_map>
#include <mutex>
#include <vector>
#include <chrono>

class Session;

// 간단한 세션 레지스트리: user_id 기준으로 마지막 세션을 관리
class SessionRegistry {
public:
    // 새 세션을 등록하고, 이전 세션(존재 시)을 반환한다.
    std::shared_ptr<Session> replace_session(const std::string& user_id,
                                             const std::shared_ptr<Session>& session);

    // 세션 종료 시 등록 해제 (매칭되는 경우에만)
    void remove_if_match(const std::string& user_id, const Session* session);

    // 모든 활성 세션 가져오기 (채굴 틱 업데이트용)
    std::vector<std::shared_ptr<Session>> get_all_sessions();

    void mark_disconnected(const std::string& user_id, const std::string& device_id);
    bool consume_grace_if_valid(const std::string& user_id,
                                const std::string& device_id,
                                std::chrono::system_clock::time_point* disconnected_at_out = nullptr);
    void clear_grace(const std::string& user_id);

private:
    struct GraceEntry {
        std::string device_id;
        std::chrono::system_clock::time_point disconnected_at{};
        std::chrono::steady_clock::time_point expires_at{};
    };

    std::unordered_map<std::string, std::weak_ptr<Session>> sessions_;
    std::unordered_map<std::string, GraceEntry> grace_sessions_;
    std::mutex mutex_;
    static constexpr std::chrono::seconds kGraceTtl{30};
};
