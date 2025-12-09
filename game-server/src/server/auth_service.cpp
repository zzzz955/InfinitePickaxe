#include "auth_service.h"

AuthService::AuthService(std::string auth_host, unsigned short auth_port, RedisClient& redis)
    : auth_host_(std::move(auth_host)), auth_port_(auth_port), redis_(redis) {}

VerifyResult AuthService::verify_and_cache(const std::string& jwt, const std::string& client_ip) {
    VerifyResult vr = verify_jwt_with_auth(auth_host_, auth_port_, jwt);
    if (vr.valid && !vr.is_banned && !vr.user_id.empty()) {
        redis_.set_session(vr.user_id, vr.expires_at, vr.device_id, client_ip);
    }
    return vr;
}
