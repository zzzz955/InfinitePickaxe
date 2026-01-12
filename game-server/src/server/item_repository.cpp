#include "item_repository.h"
#include <algorithm>
#include <pqxx/pqxx>
#include <spdlog/spdlog.h>

bool ItemRepository::ensure_inventory(const std::string& user_id, uint32_t base_capacity) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        tx.exec_params(
            "INSERT INTO game_schema.user_item_inventory (user_id, current_capacity) "
            "VALUES ($1::uuid, $2) ON CONFLICT (user_id) DO NOTHING",
            user_id, static_cast<int32_t>(base_capacity));
        tx.commit();
        return true;
    } catch (const std::exception& ex) {
        spdlog::error("ensure_item_inventory failed: user={} error={}", user_id, ex.what());
        return false;
    }
}

ItemInventoryState ItemRepository::get_inventory_state(const std::string& user_id) {
    ItemInventoryState state;
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        auto result = tx.exec_params(
            "SELECT i.current_capacity, "
            "  COALESCE((SELECT COUNT(*) FROM game_schema.user_items "
            "    WHERE user_id = $1::uuid AND count > 0), 0) AS stack_slots, "
            "  COALESCE((SELECT COUNT(*) FROM game_schema.user_item_instances "
            "    WHERE user_id = $1::uuid), 0) AS instance_slots "
            "FROM game_schema.user_item_inventory i "
            "WHERE i.user_id = $1::uuid",
            user_id);

        if (!result.empty()) {
            state.current_capacity = result[0][0].as<uint32_t>();
            state.stack_slots = result[0][1].as<uint32_t>();
            state.instance_slots = result[0][2].as<uint32_t>();
            state.used_slots = state.stack_slots + state.instance_slots;
        }

        tx.commit();
    } catch (const std::exception& ex) {
        spdlog::error("get_item_inventory_state failed: user={} error={}", user_id, ex.what());
    }
    return state;
}

std::vector<ItemStackData> ItemRepository::get_user_items(const std::string& user_id) {
    std::vector<ItemStackData> items;
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        auto result = tx.exec_params(
            "SELECT item_id, count "
            "FROM game_schema.user_items "
            "WHERE user_id = $1::uuid AND count > 0 "
            "ORDER BY item_id ASC",
            user_id);

        for (const auto& row : result) {
            ItemStackData entry;
            entry.item_id = row[0].as<uint32_t>();
            entry.count = static_cast<uint64_t>(row[1].as<int64_t>());
            items.push_back(entry);
        }

        tx.commit();
    } catch (const std::exception& ex) {
        spdlog::error("get_user_items failed: user={} error={}", user_id, ex.what());
    }
    return items;
}

std::vector<ItemInstanceData> ItemRepository::get_user_item_instances(const std::string& user_id) {
    std::vector<ItemInstanceData> items;
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        auto result = tx.exec_params(
            "SELECT item_instance_id::text, item_id, "
            "  FLOOR(EXTRACT(EPOCH FROM acquired_at) * 1000)::BIGINT AS acquired_at_ms "
            "FROM game_schema.user_item_instances "
            "WHERE user_id = $1::uuid "
            "ORDER BY item_id ASC, acquired_at ASC, item_instance_id ASC",
            user_id);

        for (const auto& row : result) {
            ItemInstanceData entry;
            entry.item_instance_id = row[0].as<std::string>();
            entry.item_id = row[1].as<uint32_t>();
            entry.acquired_at = row[2].as<uint64_t>();
            items.push_back(entry);
        }

        tx.commit();
    } catch (const std::exception& ex) {
        spdlog::error("get_user_item_instances failed: user={} error={}", user_id, ex.what());
    }
    return items;
}

ItemAddResult ItemRepository::add_stack_item(const std::string& user_id, uint32_t item_id,
                                             uint64_t add_count, uint32_t max_stack) {
    ItemAddResult result;
    if (add_count == 0) {
        result.success = true;
        return result;
    }
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        auto inv_row = tx.exec_params1(
            "SELECT current_capacity FROM game_schema.user_item_inventory "
            "WHERE user_id = $1::uuid FOR UPDATE",
            user_id);
        uint32_t current_capacity = inv_row[0].as<uint32_t>();

        auto count_rows = tx.exec_params(
            "SELECT count FROM game_schema.user_items "
            "WHERE user_id = $1::uuid AND item_id = $2 FOR UPDATE",
            user_id, static_cast<int32_t>(item_id));

        uint64_t existing_count = 0;
        bool has_stack = false;
        if (!count_rows.empty()) {
            existing_count = static_cast<uint64_t>(count_rows[0][0].as<int64_t>());
            has_stack = existing_count > 0;
        }

        auto slots_row = tx.exec_params1(
            "SELECT "
            "  COALESCE((SELECT COUNT(*) FROM game_schema.user_items "
            "    WHERE user_id = $1::uuid AND count > 0), 0) AS stack_slots, "
            "  COALESCE((SELECT COUNT(*) FROM game_schema.user_item_instances "
            "    WHERE user_id = $1::uuid), 0) AS instance_slots",
            user_id);

        uint32_t stack_slots = slots_row[0].as<uint32_t>();
        uint32_t instance_slots = slots_row[1].as<uint32_t>();
        uint32_t used_slots = stack_slots + instance_slots;

        bool needs_slot = !has_stack;
        result.current_capacity = current_capacity;
        result.used_slots = used_slots;

        if (needs_slot && used_slots >= current_capacity) {
            result.inventory_full = true;
            return result;
        }

        uint64_t new_count = existing_count + add_count;
        if (max_stack > 0 && new_count > max_stack) {
            result.stack_limit_reached = true;
            return result;
        }

        if (count_rows.empty()) {
            tx.exec_params(
                "INSERT INTO game_schema.user_items (user_id, item_id, count) "
                "VALUES ($1::uuid, $2, $3)",
                user_id, static_cast<int32_t>(item_id), static_cast<int64_t>(new_count));
        } else {
            tx.exec_params(
                "UPDATE game_schema.user_items "
                "SET count = $3 "
                "WHERE user_id = $1::uuid AND item_id = $2",
                user_id, static_cast<int32_t>(item_id), static_cast<int64_t>(new_count));
        }

        tx.commit();

        result.success = true;
        result.new_count = new_count;
        if (needs_slot) {
            result.used_slots = used_slots + 1;
        }
    } catch (const std::exception& ex) {
        spdlog::error("add_stack_item failed: user={} item={} error={}", user_id, item_id, ex.what());
    }
    return result;
}

ItemAddResult ItemRepository::add_instance_items(const std::string& user_id, uint32_t item_id,
                                                 uint32_t add_count) {
    ItemAddResult result;
    if (add_count == 0) {
        result.success = true;
        return result;
    }
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        auto inv_row = tx.exec_params1(
            "SELECT current_capacity FROM game_schema.user_item_inventory "
            "WHERE user_id = $1::uuid FOR UPDATE",
            user_id);
        uint32_t current_capacity = inv_row[0].as<uint32_t>();

        auto slots_row = tx.exec_params1(
            "SELECT "
            "  COALESCE((SELECT COUNT(*) FROM game_schema.user_items "
            "    WHERE user_id = $1::uuid AND count > 0), 0) AS stack_slots, "
            "  COALESCE((SELECT COUNT(*) FROM game_schema.user_item_instances "
            "    WHERE user_id = $1::uuid), 0) AS instance_slots",
            user_id);

        uint32_t stack_slots = slots_row[0].as<uint32_t>();
        uint32_t instance_slots = slots_row[1].as<uint32_t>();
        uint32_t used_slots = stack_slots + instance_slots;

        result.current_capacity = current_capacity;
        result.used_slots = used_slots;

        if (used_slots + add_count > current_capacity) {
            result.inventory_full = true;
            return result;
        }

        auto created = tx.exec_params(
            "INSERT INTO game_schema.user_item_instances (user_id, item_id) "
            "SELECT $1::uuid, $2 FROM generate_series(1, $3) "
            "RETURNING item_instance_id::text, item_id, "
            "  FLOOR(EXTRACT(EPOCH FROM acquired_at) * 1000)::BIGINT AS acquired_at_ms",
            user_id, static_cast<int32_t>(item_id), static_cast<int32_t>(add_count));

        result.created_instances.reserve(created.size());
        for (const auto& row : created) {
            ItemInstanceData entry;
            entry.item_instance_id = row[0].as<std::string>();
            entry.item_id = row[1].as<uint32_t>();
            entry.acquired_at = row[2].as<uint64_t>();
            result.created_instances.push_back(entry);
        }

        tx.commit();

        result.success = true;
        result.used_slots = used_slots + add_count;
    } catch (const std::exception& ex) {
        spdlog::error("add_instance_items failed: user={} item={} error={}", user_id, item_id, ex.what());
    }
    return result;
}

ItemConsumeResult ItemRepository::consume_stack_item(const std::string& user_id, uint32_t item_id,
                                                     uint64_t consume_count) {
    ItemConsumeResult result;
    if (consume_count == 0) {
        result.success = true;
        return result;
    }
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        auto rows = tx.exec_params(
            "SELECT count FROM game_schema.user_items "
            "WHERE user_id = $1::uuid AND item_id = $2 FOR UPDATE",
            user_id, static_cast<int32_t>(item_id));

        if (rows.empty()) {
            result.insufficient = true;
            return result;
        }

        uint64_t current_count = static_cast<uint64_t>(rows[0][0].as<int64_t>());
        if (current_count < consume_count) {
            result.insufficient = true;
            return result;
        }

        uint64_t new_count = current_count - consume_count;
        if (new_count == 0) {
            tx.exec_params(
                "DELETE FROM game_schema.user_items "
                "WHERE user_id = $1::uuid AND item_id = $2",
                user_id, static_cast<int32_t>(item_id));
        } else {
            tx.exec_params(
                "UPDATE game_schema.user_items "
                "SET count = $3 "
                "WHERE user_id = $1::uuid AND item_id = $2",
                user_id, static_cast<int32_t>(item_id), static_cast<int64_t>(new_count));
        }

        tx.commit();
        result.success = true;
        result.remaining_count = new_count;
    } catch (const std::exception& ex) {
        spdlog::error("consume_stack_item failed: user={} item={} error={}", user_id, item_id, ex.what());
    }
    return result;
}

ItemConsumeResult ItemRepository::consume_instance_items(const std::string& user_id, uint32_t item_id,
                                                         uint32_t consume_count) {
    ItemConsumeResult result;
    if (consume_count == 0) {
        result.success = true;
        return result;
    }
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        auto rows = tx.exec_params(
            "SELECT item_instance_id::text FROM game_schema.user_item_instances "
            "WHERE user_id = $1::uuid AND item_id = $2 "
            "ORDER BY acquired_at ASC, item_instance_id ASC "
            "LIMIT $3 FOR UPDATE",
            user_id, static_cast<int32_t>(item_id), static_cast<int32_t>(consume_count));

        if (rows.size() < consume_count) {
            result.insufficient = true;
            return result;
        }

        std::vector<std::string> instance_ids;
        instance_ids.reserve(rows.size());
        for (const auto& row : rows) {
            instance_ids.push_back(row[0].as<std::string>());
        }

        tx.exec_params(
            "DELETE FROM game_schema.user_item_instances "
            "WHERE item_instance_id = ANY($1::uuid[])",
            instance_ids);

        tx.commit();
        result.success = true;
        result.removed_instance_ids = std::move(instance_ids);
    } catch (const std::exception& ex) {
        spdlog::error("consume_instance_items failed: user={} item={} error={}", user_id, item_id, ex.what());
    }
    return result;
}

ItemInventoryExpandResult ItemRepository::expand_inventory(const std::string& user_id, uint32_t crystal_cost,
                                                           uint32_t max_capacity, uint32_t expand_step) {
    ItemInventoryExpandResult result;
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        auto inv_row = tx.exec_params1(
            "SELECT current_capacity FROM game_schema.user_item_inventory "
            "WHERE user_id = $1::uuid FOR UPDATE",
            user_id);

        uint32_t current_capacity = inv_row[0].as<uint32_t>();
        if (current_capacity >= max_capacity) {
            result.max_capacity_reached = true;
            return result;
        }

        auto crystal_row = tx.exec_params(
            "UPDATE game_schema.user_game_data "
            "SET crystal = crystal - $2 "
            "WHERE user_id = $1::uuid AND crystal >= $2 "
            "RETURNING crystal",
            user_id, static_cast<int32_t>(crystal_cost));

        if (crystal_row.empty()) {
            result.insufficient_crystal = true;
            return result;
        }

        result.remaining_crystal = crystal_row[0][0].as<uint32_t>();

        uint32_t new_capacity = std::min(current_capacity + expand_step, max_capacity);
        tx.exec_params(
            "UPDATE game_schema.user_item_inventory "
            "SET current_capacity = $2 "
            "WHERE user_id = $1::uuid",
            user_id, static_cast<int32_t>(new_capacity));

        tx.commit();
        result.success = true;
        result.new_capacity = new_capacity;
    } catch (const std::exception& ex) {
        spdlog::error("expand_item_inventory failed: user={} error={}", user_id, ex.what());
    }
    return result;
}
