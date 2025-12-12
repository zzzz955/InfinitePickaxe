#pragma once
#include <memory>
#include <string>
#include <unordered_map>
#include <mutex>

class Session;

// 간단한 세션 레지스트리: user_id 기준으로 마지막 세션을 관리
class SessionRegistry {
public:
    // 새 세션을 등록하고, 이전 세션(존재 시)을 반환한다.
    std::shared_ptr<Session> replace_session(const std::string& user_id,
                                             const std::shared_ptr<Session>& session);

    // 세션 종료 시 등록 해제 (매칭되는 경우에만)
    void remove_if_match(const std::string& user_id, const Session* session);

private:
    std::unordered_map<std::string, std::weak_ptr<Session>> sessions_;
    std::mutex mutex_;
};
