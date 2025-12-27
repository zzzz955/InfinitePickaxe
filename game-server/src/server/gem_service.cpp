#include "gem_service.h"
#include <spdlog/spdlog.h>
#include <random>
#include <cmath>
#include <set>

namespace {
// 랜덤 넘버 생성기
std::random_device rd;
std::mt19937 gen(rd());

// DPS 계산 (slot_service.cpp와 동일)
uint64_t compute_expected_dps(uint64_t attack_power, uint32_t attack_speed_x100,
                              uint32_t crit_percent, uint32_t crit_damage) {
    double attack_speed = static_cast<double>(attack_speed_x100) / 100.0;
    double crit_rate = static_cast<double>(crit_percent) / 10000.0;
    double crit_mult = static_cast<double>(crit_damage) / 10000.0;
    double expected = static_cast<double>(attack_power) * attack_speed *
                      (1.0 + crit_rate * (crit_mult - 1.0));
    return static_cast<uint64_t>(std::llround(expected));
}
}

infinitepickaxe::GemListResponse GemService::handle_gem_list(const std::string& user_id) {
    infinitepickaxe::GemListResponse response;

    auto gems = gem_repo_.get_user_gems(user_id);
    for (const auto& gem : gems) {
        auto* gem_info = response.add_gems();
        populate_gem_info(gem, gem_info);
    }

    uint32_t capacity = gem_repo_.get_inventory_capacity(user_id);
    response.set_total_gems(static_cast<uint32_t>(gems.size()));
    response.set_inventory_capacity(capacity);

    spdlog::debug("handle_gem_list: user={} gems={} capacity={}", user_id, gems.size(), capacity);
    return response;
}

infinitepickaxe::GemGachaResult GemService::handle_gacha_pull(const std::string& user_id, uint32_t pull_count) {
    infinitepickaxe::GemGachaResult result;

    // pull_count 검증 (1 또는 11)
    const auto& gacha_meta = meta_.gem_gacha();
    uint32_t crystal_cost = 0;
    if (pull_count == 1) {
        crystal_cost = gacha_meta.single_pull_cost;
    } else if (pull_count == gacha_meta.multi_pull_count) {
        crystal_cost = gacha_meta.multi_pull_cost;
    } else {
        result.set_success(false);
        result.set_error_code("INVALID_PULL_COUNT");
        return result;
    }

    // 가중치 랜덤으로 gem_id 선택
    std::vector<uint32_t> selected_gem_ids;
    for (uint32_t i = 0; i < pull_count; ++i) {
        uint32_t gem_id = select_random_gem_by_grade_rate(gacha_meta.grade_rates);
        selected_gem_ids.push_back(gem_id);
    }

    // Repository 호출
    auto gacha_result = gem_repo_.gacha_pull(user_id, crystal_cost, selected_gem_ids);

    if (!gacha_result.success) {
        result.set_success(false);
        if (gacha_result.insufficient_crystal) {
            result.set_error_code("INSUFFICIENT_CRYSTAL");
        } else if (gacha_result.inventory_full) {
            result.set_error_code("INVENTORY_FULL");
        } else {
            result.set_error_code("DB_ERROR");
        }
        return result;
    }

    // 성공 시 결과 변환
    for (const auto& gem : gacha_result.created_gems) {
        auto* gem_info = result.add_gems();
        populate_gem_info(gem, gem_info);
    }

    result.set_success(true);
    result.set_crystal_spent(crystal_cost);
    result.set_remaining_crystal(gacha_result.remaining_crystal);

    uint32_t capacity = gem_repo_.get_inventory_capacity(user_id);
    auto all_gems = gem_repo_.get_user_gems(user_id);
    result.set_total_gems(static_cast<uint32_t>(all_gems.size()));
    result.set_inventory_capacity(capacity);

    spdlog::info("handle_gacha_pull: user={} pull_count={} cost={} gems={}",
                 user_id, pull_count, crystal_cost, gacha_result.created_gems.size());
    return result;
}

infinitepickaxe::GemSynthesisResult GemService::handle_synthesis(const std::string& user_id,
                                                                  const std::vector<std::string>& gem_instance_ids) {
    infinitepickaxe::GemSynthesisResult result;

    // 정확히 3개인지 검증
    if (gem_instance_ids.size() != 3) {
        result.set_success(false);
        result.set_error_code("INVALID_GEM_COUNT");
        return result;
    }

    // 각 gem 조회하여 grade_id, type_id 확인
    std::vector<GemInstanceData> gems;
    for (const auto& gem_id : gem_instance_ids) {
        auto gem_opt = gem_repo_.get_gem_by_instance_id(gem_id);
        if (!gem_opt.has_value()) {
            result.set_success(false);
            result.set_error_code("GEM_NOT_FOUND");
            return result;
        }
        gems.push_back(gem_opt.value());
    }

    // 모두 같은 grade, type인지 검증
    const auto* first_def = meta_.gem_definition(gems[0].gem_id);
    if (!first_def) {
        result.set_success(false);
        result.set_error_code("INVALID_GEM_METADATA");
        return result;
    }

    uint32_t grade_id = first_def->grade_id;
    uint32_t type_id = first_def->type_id;

    for (size_t i = 1; i < gems.size(); ++i) {
        const auto* def = meta_.gem_definition(gems[i].gem_id);
        if (!def || def->grade_id != grade_id || def->type_id != type_id) {
            result.set_success(false);
            result.set_error_code("GRADE_TYPE_MISMATCH");
            return result;
        }
    }

    // grade_id를 grade string으로 변환
    const auto* grade = meta_.gem_grade(grade_id);
    if (!grade) {
        result.set_success(false);
        result.set_error_code("INVALID_GRADE_METADATA");
        return result;
    }

    // 합성 규칙 조회
    const GemSynthesisRule* rule = nullptr;
    for (const auto& r : meta_.gem_synthesis_rules()) {
        if (r.from_grade == grade->grade) {
            rule = &r;
            break;
        }
    }

    if (!rule) {
        result.set_success(false);
        result.set_error_code("NO_SYNTHESIS_RULE");
        return result;
    }

    // 확률 판정
    std::uniform_int_distribution<> dist(0, 9999);
    uint32_t roll = dist(gen);
    bool synthesis_success = roll < rule->success_rate_percent;

    // 결과 gem_id 계산
    uint32_t result_gem_id = 0;
    if (synthesis_success) {
        // to_grade string을 grade_id로 변환
        uint32_t to_grade_id = 0;
        for (const auto& g : meta_.gem_grades()) {
            if (g.grade == rule->to_grade) {
                to_grade_id = g.id;
                break;
            }
        }

        if (to_grade_id == 0) {
            result.set_success(false);
            result.set_error_code("INVALID_TO_GRADE");
            return result;
        }

        // 다음 등급의 같은 타입 gem_id 찾기
        for (const auto& def : meta_.gem_definitions()) {
            if (def.grade_id == to_grade_id && def.type_id == type_id) {
                result_gem_id = def.gem_id;
                break;
            }
        }

        if (result_gem_id == 0) {
            result.set_success(false);
            result.set_error_code("RESULT_GEM_NOT_FOUND");
            return result;
        }
    }

    // Repository 호출
    auto synth_result = gem_repo_.synthesize_gems(user_id, gem_instance_ids, result_gem_id);

    if (!synth_result.success) {
        result.set_success(false);
        if (synth_result.invalid_gems) {
            result.set_error_code("INVALID_GEMS");
        } else {
            result.set_error_code("DB_ERROR");
        }
        return result;
    }

    result.set_success(true);
    result.set_synthesis_success(synthesis_success);

    if (synthesis_success && synth_result.result_gem.has_value()) {
        auto* gem_info = result.mutable_result_gem();
        populate_gem_info(synth_result.result_gem.value(), gem_info);
    }

    auto all_gems = gem_repo_.get_user_gems(user_id);
    result.set_total_gems(static_cast<uint32_t>(all_gems.size()));

    spdlog::info("handle_synthesis: user={} gems={} success={}",
                 user_id, gem_instance_ids.size(), synthesis_success);
    return result;
}

infinitepickaxe::GemConversionResult GemService::handle_conversion(const std::string& user_id,
                                                                    const std::string& gem_instance_id,
                                                                    infinitepickaxe::GemType target_type,
                                                                    bool use_fixed_cost) {
    infinitepickaxe::GemConversionResult result;

    // gem 조회
    auto gem_opt = gem_repo_.get_gem_by_instance_id(gem_instance_id);
    if (!gem_opt.has_value()) {
        result.set_success(false);
        result.set_error_code("GEM_NOT_FOUND");
        return result;
    }

    const auto& gem = gem_opt.value();
    const auto* gem_def = meta_.gem_definition(gem.gem_id);
    if (!gem_def) {
        result.set_success(false);
        result.set_error_code("INVALID_GEM_METADATA");
        return result;
    }

    // 현재 타입 확인
    const auto* current_type = meta_.gem_type(gem_def->type_id);
    if (!current_type) {
        result.set_success(false);
        result.set_error_code("INVALID_TYPE_METADATA");
        return result;
    }

    // 타입 변환 (enum -> string)
    std::string target_type_str;
    switch (target_type) {
        case infinitepickaxe::ATTACK_SPEED: target_type_str = "ATTACK_SPEED"; break;
        case infinitepickaxe::CRIT_RATE: target_type_str = "CRIT_RATE"; break;
        case infinitepickaxe::CRIT_DMG: target_type_str = "CRIT_DMG"; break;
        default:
            result.set_success(false);
            result.set_error_code("INVALID_TARGET_TYPE");
            return result;
    }

    // 같은 타입인지 확인
    if (current_type->type == target_type_str) {
        result.set_success(false);
        result.set_error_code("SAME_TYPE");
        return result;
    }

    // 비용 계산
    const GemConversionCost* cost_meta = nullptr;
    for (const auto& c : meta_.gem_conversion_costs()) {
        if (c.grade_id == gem_def->grade_id) {
            cost_meta = &c;
            break;
        }
    }

    if (!cost_meta) {
        result.set_success(false);
        result.set_error_code("NO_CONVERSION_COST");
        return result;
    }

    uint32_t crystal_cost = use_fixed_cost ? cost_meta->fixed_cost : cost_meta->random_cost;

    // 새로운 gem_id 계산 (같은 grade, 다른 type)
    uint32_t target_type_id = 0;
    for (const auto& t : meta_.gem_types()) {
        if (t.type == target_type_str) {
            target_type_id = t.id;
            break;
        }
    }

    if (target_type_id == 0) {
        result.set_success(false);
        result.set_error_code("TARGET_TYPE_NOT_FOUND");
        return result;
    }

    uint32_t new_gem_id = 0;
    for (const auto& def : meta_.gem_definitions()) {
        if (def.grade_id == gem_def->grade_id && def.type_id == target_type_id) {
            new_gem_id = def.gem_id;
            break;
        }
    }

    if (new_gem_id == 0) {
        result.set_success(false);
        result.set_error_code("NEW_GEM_NOT_FOUND");
        return result;
    }

    // Repository 호출
    auto conv_result = gem_repo_.convert_gem_type(gem_instance_id, new_gem_id, crystal_cost);

    if (!conv_result.success) {
        result.set_success(false);
        if (conv_result.insufficient_crystal) {
            result.set_error_code("INSUFFICIENT_CRYSTAL");
        } else if (conv_result.gem_not_found) {
            result.set_error_code("GEM_NOT_FOUND");
        } else {
            result.set_error_code("DB_ERROR");
        }
        return result;
    }

    // 변환된 gem 조회
    auto converted_gem = gem_repo_.get_gem_by_instance_id(gem_instance_id);
    if (converted_gem.has_value()) {
        auto* gem_info = result.mutable_converted_gem();
        populate_gem_info(converted_gem.value(), gem_info);
    }

    result.set_success(true);
    result.set_crystal_spent(crystal_cost);
    result.set_remaining_crystal(conv_result.remaining_crystal);

    spdlog::info("handle_conversion: user={} gem={} target_type={} cost={}",
                 user_id, gem_instance_id, target_type_str, crystal_cost);
    return result;
}

infinitepickaxe::GemDiscardResult GemService::handle_discard(const std::string& user_id,
                                                              const std::vector<std::string>& gem_instance_ids) {
    infinitepickaxe::GemDiscardResult result;

    if (gem_instance_ids.empty()) {
        result.set_success(false);
        result.set_error_code("NO_GEMS_TO_DISCARD");
        return result;
    }

    // 각 gem의 grade_id 조회하여 총 보상 계산
    uint32_t total_crystal_reward = 0;
    for (const auto& gem_id : gem_instance_ids) {
        auto gem_opt = gem_repo_.get_gem_by_instance_id(gem_id);
        if (!gem_opt.has_value()) {
            result.set_success(false);
            result.set_error_code("GEM_NOT_FOUND");
            return result;
        }

        const auto* gem_def = meta_.gem_definition(gem_opt->gem_id);
        if (!gem_def) {
            result.set_success(false);
            result.set_error_code("INVALID_GEM_METADATA");
            return result;
        }

        // 보상 조회
        const GemDiscardReward* reward_meta = nullptr;
        for (const auto& r : meta_.gem_discard_rewards()) {
            if (r.grade_id == gem_def->grade_id) {
                reward_meta = &r;
                break;
            }
        }

        if (!reward_meta) {
            result.set_success(false);
            result.set_error_code("NO_DISCARD_REWARD");
            return result;
        }

        total_crystal_reward += reward_meta->crystal_reward;
    }

    // Repository 호출
    auto discard_result = gem_repo_.discard_gems(user_id, gem_instance_ids, total_crystal_reward);

    if (!discard_result.success) {
        result.set_success(false);
        result.set_error_code("DB_ERROR");
        return result;
    }

    result.set_success(true);
    result.set_crystal_earned(discard_result.crystal_earned);
    result.set_total_crystal(discard_result.total_crystal);

    auto all_gems = gem_repo_.get_user_gems(user_id);
    result.set_total_gems(static_cast<uint32_t>(all_gems.size()));

    spdlog::info("handle_discard: user={} gems={} crystal_earned={}",
                 user_id, gem_instance_ids.size(), discard_result.crystal_earned);
    return result;
}

infinitepickaxe::GemEquipResult GemService::handle_equip(const std::string& user_id,
                                                          uint32_t pickaxe_slot_index,
                                                          uint32_t gem_slot_index,
                                                          const std::string& gem_instance_id) {
    infinitepickaxe::GemEquipResult result;

    // pickaxe_slot_id 조회
    auto slot_opt = slot_repo_.get_slot(user_id, pickaxe_slot_index);
    if (!slot_opt.has_value()) {
        result.set_success(false);
        result.set_error_code("PICKAXE_SLOT_NOT_FOUND");
        return result;
    }

    const auto& slot = slot_opt.value();

    // Repository 호출
    bool equip_success = gem_repo_.equip_gem(slot.slot_id, gem_slot_index, gem_instance_id);
    if (!equip_success) {
        result.set_success(false);
        result.set_error_code("EQUIP_FAILED");
        return result;
    }

    // 보석 보너스 계산
    auto gem_bonus = calculate_pickaxe_stats_with_gems(slot.slot_id);

    // 기본 스탯 + 보석 보너스 적용
    PickaxeSlot updated_slot = slot;
    updated_slot.attack_speed_x100 = slot.attack_speed_x100 + gem_bonus.attack_speed_x100;
    updated_slot.critical_hit_percent = slot.critical_hit_percent + gem_bonus.critical_hit_percent;
    updated_slot.critical_damage = slot.critical_damage + gem_bonus.critical_damage;

    // DPS 재계산
    updated_slot.dps = compute_expected_dps(updated_slot.attack_power, updated_slot.attack_speed_x100,
                                            updated_slot.critical_hit_percent, updated_slot.critical_damage);

    // DB 업데이트
    slot_repo_.update_slot(user_id, pickaxe_slot_index,
                          updated_slot.level, updated_slot.tier,
                          updated_slot.attack_power, updated_slot.attack_speed_x100,
                          updated_slot.critical_hit_percent, updated_slot.critical_damage,
                          updated_slot.dps, updated_slot.pity_bonus);

    // 장착된 gem 정보 조회
    auto equipped_gem = gem_repo_.get_gem_by_instance_id(gem_instance_id);
    if (equipped_gem.has_value()) {
        auto* gem_info = result.mutable_equipped_gem();
        populate_gem_info(equipped_gem.value(), gem_info);
    }

    // PickaxeSlotInfo 변환
    auto* slot_info = result.mutable_updated_pickaxe();
    populate_pickaxe_slot_info(updated_slot, slot_info);

    // total_dps 계산
    auto all_slots = slot_repo_.get_user_slots(user_id);
    uint64_t total_dps = 0;
    for (const auto& s : all_slots) {
        total_dps += s.dps;
    }

    result.set_success(true);
    result.set_pickaxe_slot_index(pickaxe_slot_index);
    result.set_gem_slot_index(gem_slot_index);
    result.set_new_total_dps(total_dps);

    spdlog::info("handle_equip: user={} pickaxe_slot={} gem_slot={} gem={}",
                 user_id, pickaxe_slot_index, gem_slot_index, gem_instance_id);
    return result;
}

infinitepickaxe::GemUnequipResult GemService::handle_unequip(const std::string& user_id,
                                                              uint32_t pickaxe_slot_index,
                                                              uint32_t gem_slot_index) {
    infinitepickaxe::GemUnequipResult result;

    // pickaxe_slot_id 조회
    auto slot_opt = slot_repo_.get_slot(user_id, pickaxe_slot_index);
    if (!slot_opt.has_value()) {
        result.set_success(false);
        result.set_error_code("PICKAXE_SLOT_NOT_FOUND");
        return result;
    }

    const auto& slot = slot_opt.value();

    // 기존 장착 gem 정보 조회 (해제 전)
    auto gem_slots = gem_repo_.get_gem_slots_for_pickaxe(slot.slot_id);
    std::optional<GemInstanceData> unequipped_gem;
    for (const auto& gs : gem_slots) {
        if (gs.gem_slot_index == gem_slot_index && gs.equipped_gem.has_value()) {
            unequipped_gem = gs.equipped_gem;
            break;
        }
    }

    // Repository 호출
    bool unequip_success = gem_repo_.unequip_gem(slot.slot_id, gem_slot_index);
    if (!unequip_success) {
        result.set_success(false);
        result.set_error_code("UNEQUIP_FAILED");
        return result;
    }

    // 보석 보너스 계산 (해제 후)
    auto gem_bonus = calculate_pickaxe_stats_with_gems(slot.slot_id);

    // 기본 스탯 + 보석 보너스 적용
    PickaxeSlot updated_slot = slot;
    updated_slot.attack_speed_x100 = slot.attack_speed_x100 + gem_bonus.attack_speed_x100;
    updated_slot.critical_hit_percent = slot.critical_hit_percent + gem_bonus.critical_hit_percent;
    updated_slot.critical_damage = slot.critical_damage + gem_bonus.critical_damage;

    // DPS 재계산
    updated_slot.dps = compute_expected_dps(updated_slot.attack_power, updated_slot.attack_speed_x100,
                                            updated_slot.critical_hit_percent, updated_slot.critical_damage);

    // DB 업데이트
    slot_repo_.update_slot(user_id, pickaxe_slot_index,
                          updated_slot.level, updated_slot.tier,
                          updated_slot.attack_power, updated_slot.attack_speed_x100,
                          updated_slot.critical_hit_percent, updated_slot.critical_damage,
                          updated_slot.dps, updated_slot.pity_bonus);

    // 해제된 gem 정보
    if (unequipped_gem.has_value()) {
        auto* gem_info = result.mutable_unequipped_gem();
        populate_gem_info(unequipped_gem.value(), gem_info);
    }

    // PickaxeSlotInfo 변환
    auto* slot_info = result.mutable_updated_pickaxe();
    populate_pickaxe_slot_info(updated_slot, slot_info);

    // total_dps 계산
    auto all_slots = slot_repo_.get_user_slots(user_id);
    uint64_t total_dps = 0;
    for (const auto& s : all_slots) {
        total_dps += s.dps;
    }

    result.set_success(true);
    result.set_pickaxe_slot_index(pickaxe_slot_index);
    result.set_gem_slot_index(gem_slot_index);
    result.set_new_total_dps(total_dps);

    spdlog::info("handle_unequip: user={} pickaxe_slot={} gem_slot={}",
                 user_id, pickaxe_slot_index, gem_slot_index);
    return result;
}

infinitepickaxe::GemSlotUnlockResult GemService::handle_slot_unlock(const std::string& user_id,
                                                                      uint32_t pickaxe_slot_index,
                                                                      uint32_t gem_slot_index) {
    infinitepickaxe::GemSlotUnlockResult result;

    // pickaxe_slot_id 조회
    auto slot_opt = slot_repo_.get_slot(user_id, pickaxe_slot_index);
    if (!slot_opt.has_value()) {
        result.set_success(false);
        result.set_error_code("PICKAXE_SLOT_NOT_FOUND");
        return result;
    }

    const auto& slot = slot_opt.value();

    // 해금 비용 조회
    const GemSlotUnlockCost* cost_meta = nullptr;
    for (const auto& c : meta_.gem_slot_unlock_costs()) {
        if (c.slot_index == gem_slot_index) {
            cost_meta = &c;
            break;
        }
    }

    if (!cost_meta) {
        result.set_success(false);
        result.set_error_code("NO_UNLOCK_COST");
        return result;
    }

    // 순차 해금 검증: 0번부터 gem_slot_index-1번까지 모두 해금되었는지 확인
    if (gem_slot_index > 0) {
        auto all_gem_slots = gem_repo_.get_gem_slots_for_pickaxe(slot.slot_id);
        std::set<uint32_t> unlocked_indices;

        for (const auto& gs : all_gem_slots) {
            if (gs.is_unlocked) {
                unlocked_indices.insert(gs.gem_slot_index);
            }
        }

        // 0부터 gem_slot_index-1까지 모두 해금되었는지 확인
        for (uint32_t i = 0; i < gem_slot_index; ++i) {
            if (unlocked_indices.find(i) == unlocked_indices.end()) {
                result.set_success(false);
                result.set_error_code("PREVIOUS_SLOT_LOCKED");
                spdlog::warn("handle_slot_unlock: PREVIOUS_SLOT_LOCKED user={} gem_slot={} missing_slot={}",
                             user_id, gem_slot_index, i);
                return result;
            }
        }
    }

    // Repository 호출
    auto unlock_result = gem_repo_.unlock_gem_slot(slot.slot_id, gem_slot_index, cost_meta->unlock_cost_crystal);

    if (!unlock_result.success) {
        result.set_success(false);
        if (unlock_result.already_unlocked) {
            result.set_error_code("ALREADY_UNLOCKED");
        } else if (unlock_result.insufficient_crystal) {
            result.set_error_code("INSUFFICIENT_CRYSTAL");
        } else {
            result.set_error_code("DB_ERROR");
        }
        return result;
    }

    result.set_success(true);
    result.set_pickaxe_slot_index(pickaxe_slot_index);
    result.set_gem_slot_index(gem_slot_index);
    result.set_crystal_spent(cost_meta->unlock_cost_crystal);
    result.set_remaining_crystal(unlock_result.remaining_crystal);

    spdlog::info("handle_slot_unlock: user={} pickaxe_slot={} gem_slot={} cost={}",
                 user_id, pickaxe_slot_index, gem_slot_index, cost_meta->unlock_cost_crystal);
    return result;
}

infinitepickaxe::GemInventoryExpandResult GemService::handle_inventory_expand(const std::string& user_id) {
    infinitepickaxe::GemInventoryExpandResult result;

    const auto& inv_config = meta_.gem_inventory_config();

    // Repository 호출
    auto expand_result = gem_repo_.expand_inventory(user_id, inv_config.expand_cost);

    if (!expand_result.success) {
        result.set_success(false);
        if (expand_result.max_capacity_reached) {
            result.set_error_code("MAX_CAPACITY");
        } else if (expand_result.insufficient_crystal) {
            result.set_error_code("INSUFFICIENT_CRYSTAL");
        } else {
            result.set_error_code("DB_ERROR");
        }
        return result;
    }

    result.set_success(true);
    result.set_new_capacity(expand_result.new_capacity);
    result.set_crystal_spent(inv_config.expand_cost);
    result.set_remaining_crystal(expand_result.remaining_crystal);

    spdlog::info("handle_inventory_expand: user={} new_capacity={} cost={}",
                 user_id, expand_result.new_capacity, inv_config.expand_cost);
    return result;
}

// ========== Private Helpers ==========

uint32_t GemService::select_random_gem_by_grade_rate(const std::vector<GemGradeRate>& grade_rates) {
    // 가중치 합계 계산
    uint32_t total_weight = 0;
    for (const auto& rate : grade_rates) {
        total_weight += rate.rate_percent;
    }

    // 랜덤 값 생성
    std::uniform_int_distribution<> dist(0, total_weight - 1);
    uint32_t roll = dist(gen);

    // 누적 합으로 등급 선택
    uint32_t cumulative = 0;
    uint32_t selected_grade_id = 0;
    for (const auto& rate : grade_rates) {
        cumulative += rate.rate_percent;
        if (roll < cumulative) {
            selected_grade_id = rate.grade_id;
            break;
        }
    }

    if (selected_grade_id == 0) {
        // fallback: 첫 번째 등급
        selected_grade_id = grade_rates[0].grade_id;
    }

    // 해당 등급의 gem_id들 중 랜덤 선택
    std::vector<uint32_t> candidate_gem_ids;
    for (const auto& def : meta_.gem_definitions()) {
        if (def.grade_id == selected_grade_id) {
            candidate_gem_ids.push_back(def.gem_id);
        }
    }

    if (candidate_gem_ids.empty()) {
        spdlog::error("select_random_gem_by_grade_rate: no gems found for grade_id={}", selected_grade_id);
        return 1; // fallback to first gem
    }

    std::uniform_int_distribution<> gem_dist(0, static_cast<int>(candidate_gem_ids.size()) - 1);
    uint32_t gem_id = candidate_gem_ids[gem_dist(gen)];

    return gem_id;
}

void GemService::populate_gem_info(const GemInstanceData& gem, infinitepickaxe::GemInfo* gem_info) {
    gem_info->set_gem_instance_id(gem.gem_instance_id);
    gem_info->set_gem_id(gem.gem_id);
    gem_info->set_acquired_at(gem.acquired_at);

    // 메타데이터에서 상세 정보 조회
    const auto* gem_def = meta_.gem_definition(gem.gem_id);
    if (!gem_def) {
        spdlog::warn("populate_gem_info: gem_id={} not found in metadata", gem.gem_id);
        return;
    }

    gem_info->set_name(gem_def->name);
    gem_info->set_icon(gem_def->icon);
    gem_info->set_stat_multiplier(gem_def->stat_multiplier);

    // GemGrade enum 변환
    const auto* grade = meta_.gem_grade(gem_def->grade_id);
    if (grade) {
        if (grade->grade == "COMMON") gem_info->set_grade(infinitepickaxe::COMMON);
        else if (grade->grade == "RARE") gem_info->set_grade(infinitepickaxe::RARE);
        else if (grade->grade == "EPIC") gem_info->set_grade(infinitepickaxe::EPIC);
        else if (grade->grade == "HERO") gem_info->set_grade(infinitepickaxe::HERO);
        else if (grade->grade == "LEGENDARY") gem_info->set_grade(infinitepickaxe::LEGENDARY);
    }

    // GemType enum 변환
    const auto* type = meta_.gem_type(gem_def->type_id);
    if (type) {
        if (type->type == "ATTACK_SPEED") gem_info->set_type(infinitepickaxe::ATTACK_SPEED);
        else if (type->type == "CRIT_RATE") gem_info->set_type(infinitepickaxe::CRIT_RATE);
        else if (type->type == "CRIT_DMG") gem_info->set_type(infinitepickaxe::CRIT_DMG);
    }
}

PickaxeSlot GemService::calculate_pickaxe_stats_with_gems(const std::string& pickaxe_slot_id) {
    // pickaxe_slot_id로 기본 슬롯 정보 조회
    // SlotRepository에 get_slot_by_id가 없으므로, user_id와 slot_index를 찾아야 함
    // 하지만 handle_equip/unequip에서 이미 slot을 조회했으므로, 그 정보를 활용하는 것이 좋음
    // 여기서는 장착된 보석들의 스탯 보너스만 계산하고, 호출하는 쪽에서 기본 슬롯 정보와 합산

    // 장착된 보석들의 스탯 보너스 계산
    auto gem_slots = gem_repo_.get_gem_slots_for_pickaxe(pickaxe_slot_id);

    uint32_t attack_speed_bonus = 0;     // basis 100
    uint32_t crit_rate_bonus = 0;        // basis 10000
    uint32_t crit_damage_bonus = 0;      // basis 100

    for (const auto& gem_slot : gem_slots) {
        if (!gem_slot.equipped_gem.has_value()) {
            continue;
        }

        const auto& gem = gem_slot.equipped_gem.value();
        const auto* gem_def = meta_.gem_definition(gem.gem_id);
        if (!gem_def) {
            continue;
        }

        const auto* type = meta_.gem_type(gem_def->type_id);
        if (!type) {
            continue;
        }

        uint32_t multiplier = gem_def->stat_multiplier; // x100

        if (type->type == "ATTACK_SPEED") {
            attack_speed_bonus += multiplier; // x100
        } else if (type->type == "CRIT_RATE") {
            // multiplier는 x100 (예: 500 = 5%), basis 10000으로 변환 (500 -> 5000)
            crit_rate_bonus += multiplier * 100;
        } else if (type->type == "CRIT_DMG") {
            crit_damage_bonus += multiplier; // x100
        }
    }

    // 이 함수는 호출하는 쪽에서 base_slot을 전달받아 적용하도록 수정 필요
    // 임시로 빈 슬롯에 보너스만 담아 반환 (handle_equip/unequip에서 직접 적용)
    PickaxeSlot bonus_slot{};
    bonus_slot.slot_id = pickaxe_slot_id;
    bonus_slot.attack_speed_x100 = attack_speed_bonus;
    bonus_slot.critical_hit_percent = crit_rate_bonus;
    bonus_slot.critical_damage = crit_damage_bonus;

    return bonus_slot;
}

void GemService::populate_pickaxe_slot_info(const PickaxeSlot& slot, infinitepickaxe::PickaxeSlotInfo* slot_info) {
    slot_info->set_slot_index(slot.slot_index);
    slot_info->set_level(slot.level);
    slot_info->set_tier(slot.tier);
    slot_info->set_attack_power(slot.attack_power);
    slot_info->set_attack_speed_x100(slot.attack_speed_x100);
    slot_info->set_critical_hit_percent(slot.critical_hit_percent);
    slot_info->set_critical_damage(slot.critical_damage);
    slot_info->set_dps(slot.dps);
    slot_info->set_pity_bonus(slot.pity_bonus);
    slot_info->set_is_unlocked(true);

    // 보석 슬롯 정보 추가
    auto gem_slots = gem_repo_.get_gem_slots_for_pickaxe(slot.slot_id);
    for (const auto& gem_slot : gem_slots) {
        auto* gem_slot_info = slot_info->add_gem_slots();
        gem_slot_info->set_gem_slot_index(gem_slot.gem_slot_index);
        gem_slot_info->set_is_unlocked(gem_slot.is_unlocked);

        // 장착된 보석이 있는 경우
        if (gem_slot.equipped_gem.has_value()) {
            const auto& gem = gem_slot.equipped_gem.value();
            auto* gem_info = gem_slot_info->mutable_equipped_gem();
            populate_gem_info(gem, gem_info);
        }
    }
}
