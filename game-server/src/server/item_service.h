#pragma once
#include "item_repository.h"
#include "metadata/metadata_loader.h"
#include <string>

struct ItemInventorySnapshot {
    uint32_t current_capacity{0};
    uint32_t used_slots{0};
    std::vector<ItemStackData> stacks;
    std::vector<ItemInstanceData> instances;
};

class ItemService {
public:
    ItemService(ItemRepository& item_repo, const MetadataLoader& meta)
        : item_repo_(item_repo), meta_(meta) {}

    ItemInventorySnapshot handle_inventory(const std::string& user_id);
    ItemInventoryExpandResult handle_inventory_expand(const std::string& user_id);
    ItemAddResult add_item(const std::string& user_id, uint32_t item_id, uint64_t count);
    ItemConsumeResult consume_item(const std::string& user_id, uint32_t item_id, uint64_t count);

private:
    bool ensure_inventory(const std::string& user_id);

    ItemRepository& item_repo_;
    const MetadataLoader& meta_;
};
