#pragma once
#include "ad_repository.h"
#include "game_repository.h"
#include "metadata/metadata_loader.h"
#include "game.pb.h"
#include <string>
#include <vector>

class AdService {
public:
    AdService(AdRepository& repo, GameRepository& game_repo, const MetadataLoader& meta)
        : repo_(repo), game_repo_(game_repo), meta_(meta) {}

    std::vector<AdCounter> get_ad_counters(const std::string& user_id);
    AdCounter get_or_create_ad_counter(const std::string& user_id, const std::string& ad_type);
    infinitepickaxe::AdCountersState get_ad_counters_state(const std::string& user_id);
    infinitepickaxe::AdWatchResult handle_ad_watch(const std::string& user_id, const std::string& ad_type);

private:
    AdRepository& repo_;
    GameRepository& game_repo_;
    const MetadataLoader& meta_;
};
