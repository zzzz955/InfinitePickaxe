CREATE SCHEMA IF NOT EXISTS game_schema;

CREATE TABLE IF NOT EXISTS game_schema.user_item_inventory (
    user_id           UUID PRIMARY KEY,
    current_capacity  INTEGER NOT NULL DEFAULT 24 CHECK (current_capacity >= 0),
    created_at        TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at        TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS game_schema.user_items (
    user_id     UUID NOT NULL,
    item_id     INTEGER NOT NULL CHECK (item_id >= 0),
    count       BIGINT NOT NULL CHECK (count >= 0),
    created_at  TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_user_items PRIMARY KEY (user_id, item_id)
);
CREATE INDEX IF NOT EXISTS idx_user_items_user ON game_schema.user_items(user_id);

CREATE TABLE IF NOT EXISTS game_schema.user_item_instances (
    item_instance_id  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id           UUID NOT NULL,
    item_id           INTEGER NOT NULL CHECK (item_id >= 0),
    acquired_at       TIMESTAMP NOT NULL DEFAULT NOW(),
    created_at        TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at        TIMESTAMP NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_user_item_instances_user ON game_schema.user_item_instances(user_id);
CREATE INDEX IF NOT EXISTS idx_user_item_instances_item ON game_schema.user_item_instances(item_id);

DROP TRIGGER IF EXISTS trg_user_item_inventory_updated ON game_schema.user_item_inventory;
CREATE TRIGGER trg_user_item_inventory_updated
    BEFORE UPDATE ON game_schema.user_item_inventory
    FOR EACH ROW EXECUTE FUNCTION game_schema.touch_updated_at();

DROP TRIGGER IF EXISTS trg_user_items_updated ON game_schema.user_items;
CREATE TRIGGER trg_user_items_updated
    BEFORE UPDATE ON game_schema.user_items
    FOR EACH ROW EXECUTE FUNCTION game_schema.touch_updated_at();

DROP TRIGGER IF EXISTS trg_user_item_instances_updated ON game_schema.user_item_instances;
CREATE TRIGGER trg_user_item_instances_updated
    BEFORE UPDATE ON game_schema.user_item_instances
    FOR EACH ROW EXECUTE FUNCTION game_schema.touch_updated_at();
