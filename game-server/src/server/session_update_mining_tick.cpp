// Session::update_mining_tick() 구현
// 파일이 길어서 별도 파일로 작성 후 session.cpp에 추가

void Session::update_mining_tick(float delta_ms) {
    // 인증되지 않았거나 세션이 닫혔으면 무시
    if (!authenticated_ || closed_) {
        return;
    }

    // 채굴 중이 아니면 리스폰 타이머 처리
    if (!mining_state_.is_mining) {
        if (mining_state_.respawn_timer_ms > 0) {
            mining_state_.respawn_timer_ms -= delta_ms;
            if (mining_state_.respawn_timer_ms <= 0) {
                // 5초 대기 완료 → 새 광물로 자동 시작
                start_new_mineral();
            }
        }
        return;
    }

    // 채굴 시뮬레이션
    std::vector<infinitepickaxe::PickaxeAttack> attacks;

    for (auto& slot : mining_state_.slots) {
        slot.next_attack_timer_ms -= delta_ms;

        // 40ms 동안 여러 번 공격할 수 있음 (attack_speed가 매우 빠른 경우)
        while (slot.next_attack_timer_ms <= 0) {
            infinitepickaxe::PickaxeAttack attack;
            attack.set_slot_index(slot.slot_index);
            attack.set_damage(slot.attack_power);
            attacks.push_back(attack);

            // 다음 공격 시간 설정 (밀리초)
            // attack_speed = attacks per second
            // attack_interval_ms = 1000 / attack_speed
            float attack_interval_ms = 1000.0f / slot.attack_speed;
            slot.next_attack_timer_ms += attack_interval_ms;
        }
    }

    // HP 감소
    uint64_t total_damage = 0;
    for (const auto& attack : attacks) {
        total_damage += attack.damage();
    }

    if (total_damage > 0) {
        if (mining_state_.current_hp > total_damage) {
            mining_state_.current_hp -= total_damage;
        } else {
            mining_state_.current_hp = 0;
        }
    }

    // 채굴 완료 체크
    if (mining_state_.current_hp == 0) {
        // 마지막 타격까지 반영된 업데이트 전달 후 완료 통보
        send_mining_update(attacks);
        handle_mining_complete_immediate();
        return;
    }

    // MiningUpdate 전송 (공격이 없어도 전송 - 클라이언트 동기화)
    send_mining_update(attacks);
}

void Session::start_new_mineral() {
    // 새 광물로 시작 (현재는 동일 광물 재시작)
    // TODO: 광물 선택 로직 추가 가능

    // 메타데이터에서 광물 정보 조회
    const auto* mineral = metadata_.mineral(mining_state_.current_mineral_id);
    if (!mineral) {
        spdlog::error("Invalid mineral_id: {}", mining_state_.current_mineral_id);
        return;
    }

    mining_state_.current_hp = mineral->max_hp;
    mining_state_.max_hp = mineral->max_hp;
    mining_state_.is_mining = true;
    mining_state_.respawn_timer_ms = 0;

    // 슬롯 정보 로드 (DB에서)
    auto slots_response = slot_service_.handle_all_slots(user_id_);
    mining_state_.slots.clear();

    for (const auto& slot_info : slots_response.slots()) {
        if (slot_info.is_unlocked() && slot_info.level() > 0) {
            SlotMiningState slot;
            slot.slot_index = slot_info.slot_index();
            slot.attack_power = slot_info.attack_power();
            slot.attack_speed = slot_info.attack_speed_x100() / 100.0f;  // 100 → 1.0 APS

            // 초기 공격 타이머: 랜덤하게 분산 (모든 슬롯이 동시에 공격하지 않도록)
            slot.next_attack_timer_ms = (float)(std::rand() % 1000) / 1000.0f * (1000.0f / slot.attack_speed);

            mining_state_.slots.push_back(slot);
        }
    }

    spdlog::info("Mining started: user={} mineral={} hp={} slots={}",
                 user_id_, mining_state_.current_mineral_id, mining_state_.current_hp, mining_state_.slots.size());
}

void Session::send_mining_update(const std::vector<infinitepickaxe::PickaxeAttack>& attacks) {
    infinitepickaxe::MiningUpdate update;
    update.set_mineral_id(mining_state_.current_mineral_id);
    update.set_current_hp(mining_state_.current_hp);
    update.set_max_hp(mining_state_.max_hp);
    update.set_server_timestamp(static_cast<uint64_t>(
        std::chrono::duration_cast<std::chrono::milliseconds>(
            std::chrono::system_clock::now().time_since_epoch()).count()));

    for (const auto& attack : attacks) {
        *update.add_attacks() = attack;
    }

    infinitepickaxe::Envelope env;
    env.set_type(infinitepickaxe::MINING_UPDATE);
    *env.mutable_mining_update() = update;
    send_envelope(env);
}

void Session::handle_mining_complete_immediate() {
    // 채굴 완료 처리
    mining_state_.is_mining = false;

    // 메타데이터에서 보상 조회
    const auto* mineral = metadata_.mineral(mining_state_.current_mineral_id);
    if (!mineral) {
        spdlog::error("Invalid mineral_id: {}", mining_state_.current_mineral_id);
        return;
    }

    uint64_t gold_reward = mineral->reward;
    uint32_t respawn_time_sec = mineral->respawn_time;

    // DB에 채굴 완료 기록 및 골드 지급
    auto completion_result = mining_service_.handle_complete(user_id_, mining_state_.current_mineral_id);

    // 리스폰 타이머 시작
    mining_state_.respawn_timer_ms = respawn_time_sec * 1000.0f;

    // 클라이언트에게 MiningComplete 즉시 전송
    infinitepickaxe::MiningComplete complete;
    complete.set_mineral_id(mining_state_.current_mineral_id);
    complete.set_gold_earned(completion_result.gold_earned());
    complete.set_total_gold(completion_result.total_gold());
    complete.set_mining_count(completion_result.mining_count());
    complete.set_respawn_time(respawn_time_sec);
    complete.set_server_timestamp(static_cast<uint64_t>(
        std::chrono::duration_cast<std::chrono::milliseconds>(
            std::chrono::system_clock::now().time_since_epoch()).count()));

    infinitepickaxe::Envelope env;
    env.set_type(infinitepickaxe::MINING_COMPLETE);
    *env.mutable_mining_complete() = complete;
    send_envelope(env);

    spdlog::info("Mining completed: user={} mineral={} gold_earned={} respawn_time={}s",
                 user_id_, mining_state_.current_mineral_id, gold_reward, respawn_time_sec);
}
