#pragma once
#include "game.pb.h"
#include "offline_repository.h"
#include "metadata/metadata_loader.h"

class OfflineService {
public:
    OfflineService(OfflineRepository& repo, const MetadataLoader& meta)
        : repo_(repo), meta_(meta) {}
    OfflineState get_state(const std::string& user_id);
    infinitepickaxe::OfflineRewardResult handle_request(const std::string& user_id);
    std::optional<uint32_t> set_offline_seconds_today(const std::string& user_id, uint32_t seconds);
    uint32_t initial_offline_seconds() const { return meta_.offline_defaults().initial_offline_seconds; }
private:
    OfflineRepository& repo_;
    const MetadataLoader& meta_;
};
