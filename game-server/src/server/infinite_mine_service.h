#pragma once

#include "infinite_mine_repository.h"
#include "game_repository.h"
#include "metadata/metadata_loader.h"
#include "game.pb.h"
#include <string>
#include <vector>

class InfiniteMineService {
public:
    InfiniteMineService(InfiniteMineRepository& repo,
                        GameRepository& game_repo,
                        const MetadataLoader& meta)
        : repo_(repo), game_repo_(game_repo), meta_(meta) {}

    infinitepickaxe::InfiniteMineStateResponse get_state(const std::string& user_id);
    infinitepickaxe::InfiniteMineChallengeStartResult start_challenge(const std::string& user_id, uint32_t floor);
    infinitepickaxe::InfiniteMineChallengeResult handle_clear(const std::string& user_id, uint32_t floor);
    infinitepickaxe::InfiniteMineAutoClaimResult claim_auto_reward(const std::string& user_id, uint32_t floor);
    infinitepickaxe::InfiniteMineAutoClaimAllResult claim_all_auto_rewards(const std::string& user_id);
    uint32_t get_highest_cleared_floor(const std::string& user_id);

private:
    std::string kst_today_date_string() const;
    uint32_t resolve_auto_reward_divisor() const;
    bool is_auto_claimable(const InfiniteMineProgress& progress, const std::string& today) const;
    InfiniteMineRepository& repo_;
    GameRepository& game_repo_;
    const MetadataLoader& meta_;
};
