#pragma once
#include "connection_pool.h"
#include <cstdint>
#include <string>
#include <vector>

struct ItemStackData {
    uint32_t item_id{0};
    uint64_t count{0};
};

struct ItemInstanceData {
    std::string item_instance_id;
    uint32_t item_id{0};
    uint64_t acquired_at{0};
};

struct ItemInventoryState {
    uint32_t current_capacity{0};
    uint32_t used_slots{0};
    uint32_t stack_slots{0};
    uint32_t instance_slots{0};
};

struct ItemAddResult {
    bool success{false};
    bool invalid_item{false};
    bool invalid_count{false};
    bool inventory_full{false};
    bool stack_limit_reached{false};
    uint32_t current_capacity{0};
    uint32_t used_slots{0};
    uint64_t new_count{0};
    std::vector<ItemInstanceData> created_instances;
};

struct ItemConsumeResult {
    bool success{false};
    bool invalid_item{false};
    bool invalid_count{false};
    bool insufficient{false};
    uint64_t remaining_count{0};
    std::vector<std::string> removed_instance_ids;
};

struct ItemInventoryExpandResult {
    bool success{false};
    bool max_capacity_reached{false};
    bool insufficient_crystal{false};
    uint32_t new_capacity{0};
    uint32_t remaining_crystal{0};
};

class ItemRepository {
public:
    explicit ItemRepository(ConnectionPool& pool) : pool_(pool) {}

    bool ensure_inventory(const std::string& user_id, uint32_t base_capacity);
    ItemInventoryState get_inventory_state(const std::string& user_id);
    std::vector<ItemStackData> get_user_items(const std::string& user_id);
    std::vector<ItemInstanceData> get_user_item_instances(const std::string& user_id);

    ItemAddResult add_stack_item(const std::string& user_id, uint32_t item_id,
                                 uint64_t add_count, uint32_t max_stack);
    ItemAddResult add_instance_items(const std::string& user_id, uint32_t item_id,
                                     uint32_t add_count);

    ItemConsumeResult consume_stack_item(const std::string& user_id, uint32_t item_id,
                                         uint64_t consume_count);
    ItemConsumeResult consume_instance_items(const std::string& user_id, uint32_t item_id,
                                             uint32_t consume_count);

    ItemInventoryExpandResult expand_inventory(const std::string& user_id, uint32_t crystal_cost,
                                               uint32_t max_capacity, uint32_t expand_step);

private:
    ConnectionPool& pool_;
};
