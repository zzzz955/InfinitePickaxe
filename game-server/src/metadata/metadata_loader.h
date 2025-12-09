#pragma once
#include <string>
#include <vector>
#include <unordered_map>

struct PickaxeLevel {
    uint32_t level;
    uint64_t dps;
    uint64_t cost;
};

struct MineralMeta {
    uint32_t id;
    std::string name;
    uint64_t hp;
    uint64_t reward;
    uint32_t respawn_time; // seconds
};

struct MissionMeta {
    uint32_t index;
    std::string type;
    uint32_t target;
    uint32_t reward_crystal;
    std::string description;
};

class MetadataLoader {
public:
    bool load(const std::string& base_path);

    const PickaxeLevel* pickaxe_level(uint32_t level) const;
    const MineralMeta* mineral(uint32_t id) const;
    const std::vector<MissionMeta>& missions() const { return missions_; }

private:
    std::unordered_map<uint32_t, PickaxeLevel> pickaxe_levels_;
    std::unordered_map<uint32_t, MineralMeta> minerals_;
    std::vector<MissionMeta> missions_;
};
