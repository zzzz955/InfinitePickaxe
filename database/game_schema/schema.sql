-- Game schema DDL (MVP)
-- Requires: CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE SCHEMA IF NOT EXISTS game_schema;

CREATE TABLE IF NOT EXISTS game_schema.user_game_data (
    user_id               UUID PRIMARY KEY,
    gold                  BIGINT NOT NULL DEFAULT 0 CHECK (gold >= 0),
    crystal               INTEGER NOT NULL DEFAULT 0 CHECK (crystal >= 0),
    total_mining_count    BIGINT NOT NULL DEFAULT 0,
    highest_pickaxe_level INTEGER NOT NULL DEFAULT 0,
    unlocked_slots        BOOLEAN[4] NOT NULL DEFAULT ARRAY[TRUE, FALSE, FALSE, FALSE],
    total_dps             BIGINT NOT NULL DEFAULT 10 CHECK (total_dps >= 0),
    current_mineral_id    INTEGER,
    current_mineral_hp    BIGINT,
    max_offline_hours     INTEGER NOT NULL DEFAULT 3,
    cheat_score           INTEGER NOT NULL DEFAULT 0,
    created_at            TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at            TIMESTAMP NOT NULL DEFAULT NOW(),
    last_login_at         TIMESTAMP NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_user_game_gold ON game_schema.user_game_data(gold DESC);
CREATE INDEX IF NOT EXISTS idx_user_game_level ON game_schema.user_game_data(highest_pickaxe_level DESC);

CREATE TABLE IF NOT EXISTS game_schema.pickaxe_slots (
    slot_id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id          UUID NOT NULL,
    slot_index       INTEGER NOT NULL CHECK (slot_index BETWEEN 0 AND 3),
    level            INTEGER NOT NULL DEFAULT 0 CHECK (level >= 0 AND level <= 109),
    tier             INTEGER NOT NULL DEFAULT 1 CHECK (tier BETWEEN 1 AND 22),
    attack_power     BIGINT NOT NULL DEFAULT 10 CHECK (attack_power > 0),
    attack_speed_x100 INTEGER NOT NULL DEFAULT 100 CHECK (attack_speed_x100 BETWEEN 100 AND 2500),
    critical_hit_percent INTEGER NOT NULL DEFAULT 500 CHECK (critical_hit_percent BETWEEN 0 AND 10000),
    critical_damage  INTEGER NOT NULL DEFAULT 15000 CHECK (critical_damage >= 0),
    dps              BIGINT NOT NULL DEFAULT 10 CHECK (dps > 0),
    pity_bonus       INTEGER NOT NULL DEFAULT 0 CHECK (pity_bonus >= 0 AND pity_bonus <= 10000), -- basis 10000 = 100.00%
    created_at       TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at       TIMESTAMP NOT NULL DEFAULT NOW(),
    last_upgraded_at TIMESTAMP,
    CONSTRAINT uq_user_slot UNIQUE (user_id, slot_index)
);
CREATE INDEX IF NOT EXISTS idx_pickaxe_user ON game_schema.pickaxe_slots(user_id);
CREATE INDEX IF NOT EXISTS idx_pickaxe_level ON game_schema.pickaxe_slots(level DESC);

CREATE TABLE IF NOT EXISTS game_schema.user_ad_counters (
    user_id     UUID NOT NULL,
    ad_type     VARCHAR(32) NOT NULL,
    ad_count    INTEGER NOT NULL DEFAULT 0 CHECK (ad_count >= 0),
    reset_date  DATE NOT NULL DEFAULT CURRENT_DATE,
    created_at  TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_user_ad PRIMARY KEY (user_id, ad_type),
    CONSTRAINT chk_user_ad_type CHECK (ad_type IN ('upgrade_discount', 'mission_reroll', 'crystal_reward'))
);
CREATE INDEX IF NOT EXISTS idx_user_ad_reset ON game_schema.user_ad_counters(user_id, reset_date);

CREATE TABLE IF NOT EXISTS game_schema.user_mission_daily (
    user_id         UUID NOT NULL,
    mission_date    DATE NOT NULL DEFAULT CURRENT_DATE,
    assigned_count  INTEGER NOT NULL DEFAULT 0 CHECK (assigned_count BETWEEN 0 AND 7),
    reroll_count    INTEGER NOT NULL DEFAULT 0 CHECK (reroll_count >= 0),
    created_at      TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_user_mission_daily PRIMARY KEY (user_id, mission_date)
);

CREATE TABLE IF NOT EXISTS game_schema.user_mission_slots (
    user_id         UUID NOT NULL,
    slot_no         INTEGER NOT NULL CHECK (slot_no BETWEEN 1 AND 3),
    mission_id      UUID NOT NULL DEFAULT gen_random_uuid(),
    mission_type    VARCHAR(50) NOT NULL,
    target_value    INTEGER NOT NULL CHECK (target_value > 0),
    current_value   INTEGER NOT NULL DEFAULT 0 CHECK (current_value >= 0),
    reward_crystal  INTEGER NOT NULL DEFAULT 0 CHECK (reward_crystal >= 0),
    status          VARCHAR(16) NOT NULL DEFAULT 'active' CHECK (status IN ('active', 'completed', 'claimed')),
    assigned_at     TIMESTAMP NOT NULL DEFAULT NOW(),
    completed_at    TIMESTAMP,
    claimed_at      TIMESTAMP,
    expires_at      TIMESTAMP,
    created_at      TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_user_mission_slot PRIMARY KEY (user_id, slot_no),
    CONSTRAINT uq_user_mission_id UNIQUE (user_id, mission_id)
);
CREATE INDEX IF NOT EXISTS idx_mission_slots_status ON game_schema.user_mission_slots(user_id, status);
CREATE INDEX IF NOT EXISTS idx_mission_slots_expiry ON game_schema.user_mission_slots(expires_at);

-- 트리거: updated_at 자동 갱신 (테이블별)
CREATE OR REPLACE FUNCTION game_schema.touch_updated_at() RETURNS trigger AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_user_game_data_updated
    BEFORE UPDATE ON game_schema.user_game_data
    FOR EACH ROW EXECUTE FUNCTION game_schema.touch_updated_at();

CREATE TRIGGER trg_pickaxe_slots_updated
    BEFORE UPDATE ON game_schema.pickaxe_slots
    FOR EACH ROW EXECUTE FUNCTION game_schema.touch_updated_at();

CREATE TRIGGER trg_user_ad_counters_updated
    BEFORE UPDATE ON game_schema.user_ad_counters
    FOR EACH ROW EXECUTE FUNCTION game_schema.touch_updated_at();

CREATE TRIGGER trg_user_mission_daily_updated
    BEFORE UPDATE ON game_schema.user_mission_daily
    FOR EACH ROW EXECUTE FUNCTION game_schema.touch_updated_at();

CREATE TRIGGER trg_user_mission_slots_updated
    BEFORE UPDATE ON game_schema.user_mission_slots
    FOR EACH ROW EXECUTE FUNCTION game_schema.touch_updated_at();
