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

void SessionRegistry::mark_disconnected(const std::string& user_id, const std::string& device_id) {
    std::lock_guard<std::mutex> lock(mutex_);
    const auto now = std::chrono::steady_clock::now();
    for (auto it = grace_sessions_.begin(); it != grace_sessions_.end();)
    {
        if (now >= it->second.expires_at)
        {
            it = grace_sessions_.erase(it);
            continue;
        }
        ++it;
    }
    GraceEntry entry;
    entry.device_id = device_id;
    entry.disconnected_at = std::chrono::system_clock::now();
    entry.expires_at = now + kGraceTtl;
    grace_sessions_[user_id] = std::move(entry);
}

bool SessionRegistry::consume_grace_if_valid(const std::string& user_id,
                                             const std::string& device_id,
                                             std::chrono::system_clock::time_point* disconnected_at_out) {
    std::lock_guard<std::mutex> lock(mutex_);
    auto it = grace_sessions_.find(user_id);
    if (it == grace_sessions_.end()) {
        return false;
    }
    const auto now = std::chrono::steady_clock::now();
    if (now >= it->second.expires_at) {
        grace_sessions_.erase(it);
        return false;
    }
    if (!it->second.device_id.empty() && !device_id.empty() && it->second.device_id != device_id) {
        grace_sessions_.erase(it);
        return false;
    }
    if (disconnected_at_out) {
        *disconnected_at_out = it->second.disconnected_at;
    }
    grace_sessions_.erase(it);
    return true;
}

void SessionRegistry::clear_grace(const std::string& user_id) {
    std::lock_guard<std::mutex> lock(mutex_);
    grace_sessions_.erase(user_id);
}
