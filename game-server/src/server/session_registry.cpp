#include "session_registry.h"
#include "session.h"
#include <vector>

std::shared_ptr<Session> SessionRegistry::replace_session(const std::string& user_id,
                                                          const std::shared_ptr<Session>& session) {
    std::lock_guard<std::mutex> lock(mutex_);
    std::shared_ptr<Session> previous;
    auto it = sessions_.find(user_id);
    if (it != sessions_.end()) {
        previous = it->second.lock();
    }
    sessions_[user_id] = session;
    return previous;
}

void SessionRegistry::remove_if_match(const std::string& user_id, const Session* session) {
    std::lock_guard<std::mutex> lock(mutex_);
    auto it = sessions_.find(user_id);
    if (it != sessions_.end()) {
        auto cur = it->second.lock();
        if (!cur || cur.get() == session) {
            sessions_.erase(it);
        }
    }
}

std::vector<std::shared_ptr<Session>> SessionRegistry::get_all_sessions() {
    std::lock_guard<std::mutex> lock(mutex_);
    std::vector<std::shared_ptr<Session>> result;

    for (auto& pair : sessions_) {
        auto session = pair.second.lock();
        if (session) {
            result.push_back(session);
        }
    }

    return result;
}
