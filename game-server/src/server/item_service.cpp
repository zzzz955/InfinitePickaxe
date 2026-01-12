#include "item_service.h"
#include <limits>

bool ItemService::ensure_inventory(const std::string& user_id) {
    uint32_t base_capacity = meta_.item_inventory_config().base_capacity;
    if (base_capacity == 0) {
        base_capacity = 24;
    }
    return item_repo_.ensure_inventory(user_id, base_capacity);
}

ItemInventorySnapshot ItemService::handle_inventory(const std::string& user_id) {
    ItemInventorySnapshot snapshot;
    if (!ensure_inventory(user_id)) {
        return snapshot;
    }

    auto state = item_repo_.get_inventory_state(user_id);
    snapshot.current_capacity = state.current_capacity;
    snapshot.used_slots = state.used_slots;
    snapshot.stacks = item_repo_.get_user_items(user_id);
    snapshot.instances = item_repo_.get_user_item_instances(user_id);
    return snapshot;
}

ItemInventoryExpandResult ItemService::handle_inventory_expand(const std::string& user_id) {
    ItemInventoryExpandResult result;
    if (!ensure_inventory(user_id)) {
        return result;
    }

    const auto& config = meta_.item_inventory_config();
    if (config.expand_step == 0 || config.max_capacity == 0) {
        return result;
    }

    return item_repo_.expand_inventory(user_id, config.expand_cost, config.max_capacity, config.expand_step);
}

ItemAddResult ItemService::add_item(const std::string& user_id, uint32_t item_id, uint64_t count) {
    ItemAddResult result;
    if (count == 0) {
        result.invalid_count = true;
        return result;
    }
    if (!ensure_inventory(user_id)) {
        return result;
    }

    const auto* meta = meta_.item_info(item_id);
    if (!meta) {
        result.invalid_item = true;
        return result;
    }

    if (meta->stackable) {
        result = item_repo_.add_stack_item(user_id, item_id, count, meta->max_stack);
        return result;
    }

    if (count > std::numeric_limits<uint32_t>::max()) {
        result.invalid_count = true;
        return result;
    }

    result = item_repo_.add_instance_items(user_id, item_id, static_cast<uint32_t>(count));
    return result;
}

ItemConsumeResult ItemService::consume_item(const std::string& user_id, uint32_t item_id, uint64_t count) {
    ItemConsumeResult result;
    if (count == 0) {
        result.invalid_count = true;
        return result;
    }
    if (!ensure_inventory(user_id)) {
        return result;
    }

    const auto* meta = meta_.item_info(item_id);
    if (!meta) {
        result.invalid_item = true;
        return result;
    }

    if (meta->stackable) {
        result = item_repo_.consume_stack_item(user_id, item_id, count);
        return result;
    }

    if (count > std::numeric_limits<uint32_t>::max()) {
        result.invalid_count = true;
        return result;
    }

    result = item_repo_.consume_instance_items(user_id, item_id, static_cast<uint32_t>(count));
    return result;
}
