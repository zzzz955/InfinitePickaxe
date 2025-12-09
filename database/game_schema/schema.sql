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
    ad_count_today        INTEGER NOT NULL DEFAULT 0,
    ad_reset_date         DATE NOT NULL DEFAULT CURRENT_DATE,
    mission_reroll_free   INTEGER NOT NULL DEFAULT 2,
    mission_reroll_ad     INTEGER NOT NULL DEFAULT 3,
    mission_reset_date    DATE NOT NULL DEFAULT CURRENT_DATE,
    max_offline_hours     INTEGER NOT NULL DEFAULT 3,
    cheat_score           INTEGER NOT NULL DEFAULT 0,
    created_at            TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at            TIMESTAMP NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_user_game_gold ON game_schema.user_game_data(gold DESC);
CREATE INDEX IF NOT EXISTS idx_user_game_level ON game_schema.user_game_data(highest_pickaxe_level DESC);

CREATE TABLE IF NOT EXISTS game_schema.pickaxe_slots (
    slot_id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id          UUID NOT NULL,
    slot_index       INTEGER NOT NULL CHECK (slot_index BETWEEN 0 AND 3),
    level            INTEGER NOT NULL DEFAULT 0 CHECK (level >= 0 AND level <= 100),
    tier             INTEGER NOT NULL DEFAULT 1 CHECK (tier BETWEEN 1 AND 5),
    dps              BIGINT NOT NULL DEFAULT 10 CHECK (dps > 0),
    pity_bonus       INTEGER NOT NULL DEFAULT 0 CHECK (pity_bonus >= 0 AND pity_bonus <= 10000), -- basis 10000 = 100.00%
    created_at       TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at       TIMESTAMP NOT NULL DEFAULT NOW(),
    last_upgraded_at TIMESTAMP,
    CONSTRAINT uq_user_slot UNIQUE (user_id, slot_index)
);
CREATE INDEX IF NOT EXISTS idx_pickaxe_user ON game_schema.pickaxe_slots(user_id);
CREATE INDEX IF NOT EXISTS idx_pickaxe_level ON game_schema.pickaxe_slots(level DESC);

CREATE TABLE IF NOT EXISTS game_schema.mining_snapshots (
    snapshot_id       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id           UUID NOT NULL,
    mineral_id        INTEGER NOT NULL CHECK (mineral_id BETWEEN 0 AND 6),
    current_hp        BIGINT NOT NULL CHECK (current_hp >= 0),
    max_hp            BIGINT NOT NULL,
    mining_start_time TIMESTAMP NOT NULL,
    snapshot_time     TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_user_mineral UNIQUE (user_id, mineral_id)
);
CREATE INDEX IF NOT EXISTS idx_mining_snapshots_user ON game_schema.mining_snapshots(user_id);
CREATE INDEX IF NOT EXISTS idx_mining_snapshots_time ON game_schema.mining_snapshots(snapshot_time);

CREATE TABLE IF NOT EXISTS game_schema.mining_completions (
    completion_id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id                  UUID NOT NULL,
    mineral_id               INTEGER NOT NULL,
    gold_earned              BIGINT NOT NULL CHECK (gold_earned >= 0),
    mining_duration_seconds  INTEGER NOT NULL,
    completed_at             TIMESTAMP NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_mining_completions_user ON game_schema.mining_completions(user_id);
CREATE INDEX IF NOT EXISTS idx_mining_completions_time ON game_schema.mining_completions(completed_at DESC);
CREATE INDEX IF NOT EXISTS idx_mining_completions_mineral ON game_schema.mining_completions(mineral_id);

CREATE TABLE IF NOT EXISTS game_schema.daily_missions (
    mission_id      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         UUID NOT NULL,
    mission_index   INTEGER NOT NULL CHECK (mission_index BETWEEN 0 AND 6),
    mission_type    VARCHAR(50) NOT NULL,
    target_value    INTEGER NOT NULL,
    current_value   INTEGER NOT NULL DEFAULT 0,
    reward_crystal  INTEGER NOT NULL,
    is_completed    BOOLEAN NOT NULL DEFAULT FALSE,
    is_claimed      BOOLEAN NOT NULL DEFAULT FALSE,
    assigned_at     TIMESTAMP NOT NULL DEFAULT NOW(),
    completed_at    TIMESTAMP,
    claimed_at      TIMESTAMP,
    reset_at        TIMESTAMP NOT NULL,
    CONSTRAINT uq_user_mission_index UNIQUE (user_id, mission_index, reset_at)
);
CREATE INDEX IF NOT EXISTS idx_missions_user ON game_schema.daily_missions(user_id);
CREATE INDEX IF NOT EXISTS idx_missions_active ON game_schema.daily_missions(user_id, is_completed, is_claimed);
CREATE INDEX IF NOT EXISTS idx_missions_reset ON game_schema.daily_missions(reset_at);

CREATE TABLE IF NOT EXISTS game_schema.critical_transactions (
    transaction_id   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id          UUID NOT NULL,
    transaction_type VARCHAR(50) NOT NULL,
    gold_delta       BIGINT NOT NULL DEFAULT 0,
    crystal_delta    INTEGER NOT NULL DEFAULT 0,
    gold_after       BIGINT NOT NULL,
    crystal_after    INTEGER NOT NULL,
    metadata         JSONB,
    created_at       TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_critical_types CHECK (transaction_type IN ('IAP', 'SLOT_UNLOCK', 'REFUND', 'ADMIN_ADJUST'))
);
CREATE INDEX IF NOT EXISTS idx_critical_tx_user ON game_schema.critical_transactions(user_id);
CREATE INDEX IF NOT EXISTS idx_critical_tx_time ON game_schema.critical_transactions(created_at DESC);
CREATE INDEX IF NOT EXISTS idx_critical_tx_type ON game_schema.critical_transactions(transaction_type);
CREATE INDEX IF NOT EXISTS idx_critical_tx_metadata ON game_schema.critical_transactions USING GIN (metadata);
