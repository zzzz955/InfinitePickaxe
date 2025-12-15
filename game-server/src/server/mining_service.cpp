#include "mining_service.h"
#include <ctime>
#include <spdlog/spdlog.h>

// 서버 권위형 아키텍처로 변경되어 더 이상 사용하지 않음
// infinitepickaxe::MiningUpdate MiningService::handle_start(const std::string& user_id, uint32_t mineral_id) const {
//     // 광물 정보 조회
//     uint64_t max_hp = 1000;
//     if (auto m = meta_.mineral(mineral_id)) {
//         max_hp = m->hp;
//     }
//
//     // 유저 DPS 계산
//     uint64_t total_dps = calculate_user_dps(user_id);
//
//     infinitepickaxe::MiningUpdate upd;
//     upd.set_mineral_id(mineral_id);
//     upd.set_current_hp(max_hp);
//     upd.set_max_hp(max_hp);
//     upd.set_damage_dealt(0);
//     upd.set_total_dps(total_dps);
//     upd.set_server_timestamp(static_cast<uint64_t>(std::time(nullptr)));
//
//     spdlog::debug("Mining started: user={} mineral={} max_hp={} dps={}", user_id, mineral_id, max_hp, total_dps);
//     return upd;
// }
//
// infinitepickaxe::MiningUpdate MiningService::handle_sync(const std::string& user_id, uint32_t mineral_id, uint64_t client_hp) const {
//     // 광물 정보 조회
//     uint64_t max_hp = 1000;
//     if (auto m = meta_.mineral(mineral_id)) {
//         max_hp = m->hp;
//     }
//
//     // 유저 DPS 계산
//     uint64_t total_dps = calculate_user_dps(user_id);
//
//     // 1초 틱 기준 데미지 계산 (서버는 1초마다 동기화)
//     uint64_t damage_this_tick = total_dps;
//
//     // HP 감소 (0 이하로 내려가지 않도록)
//     uint64_t new_hp = (client_hp > damage_this_tick) ? (client_hp - damage_this_tick) : 0;
//
//     infinitepickaxe::MiningUpdate upd;
//     upd.set_mineral_id(mineral_id);
//     upd.set_current_hp(new_hp);
//     upd.set_max_hp(max_hp);
//     upd.set_damage_dealt(damage_this_tick);
//     upd.set_total_dps(total_dps);
//     upd.set_server_timestamp(static_cast<uint64_t>(std::time(nullptr)));
//
//     spdlog::debug("Mining sync: user={} mineral={} hp={}/{} damage={} dps={}",
//                   user_id, mineral_id, new_hp, max_hp, damage_this_tick, total_dps);
//     return upd;
// }

infinitepickaxe::MiningComplete MiningService::handle_complete(const std::string& user_id, uint32_t mineral_id) const {
    uint64_t reward = 0;
    uint32_t respawn = 5;
    if (auto m = meta_.mineral(mineral_id)) {
        reward = m->reward;
        respawn = m->respawn_time;
    }

    // DB에 완료 기록 및 골드 지급
    auto res = repo_.record_completion(user_id, mineral_id, reward);

    infinitepickaxe::MiningComplete comp;
    comp.set_mineral_id(mineral_id);
    comp.set_gold_earned(reward);
    comp.set_total_gold(res.total_gold);
    comp.set_mining_count(res.mining_count);
    comp.set_respawn_time(respawn);
    comp.set_server_timestamp(static_cast<uint64_t>(std::time(nullptr)));

    spdlog::info("Mining completed: user={} mineral={} gold_earned={} total_gold={}",
                 user_id, mineral_id, reward, res.total_gold);
    return comp;
}

uint64_t MiningService::calculate_user_dps(const std::string& user_id) const {
    // total_dps 캐시를 활용하여 성능 최적화
    auto game_data = game_repo_.get_user_game_data(user_id);
    return game_data.total_dps;
}
