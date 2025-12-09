#pragma once
#include <string>
#include <functional>
#include <unordered_map>
#include "game.pb.h"

// 각 메시지 타입을 핸들러에 매핑하는 간단한 라우터
class MessageRouter {
public:
    using HandlerFn = std::function<void(const infinitepickaxe::Envelope&)>;

    void register_handler(const std::string& msg_type, HandlerFn fn) {
        handlers_[msg_type] = std::move(fn);
    }

    bool dispatch(const infinitepickaxe::Envelope& env) const {
        auto it = handlers_.find(env.msg_type());
        if (it == handlers_.end()) return false;
        it->second(env);
        return true;
    }

private:
    std::unordered_map<std::string, HandlerFn> handlers_;
};
