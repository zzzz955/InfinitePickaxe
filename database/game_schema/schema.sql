-- Game schema DDL (clean rebuild)
-- 데이터가 없다는 전제. 기존 객체를 모두 정리한 뒤 재생성한다.

-- Drop existing triggers/functions (if any)
DROP TRIGGER IF EXISTS trg_user_game_data_updated ON game_schema.user_game_data;
DROP TRIGGER IF EXISTS trg_pickaxe_slots_updated ON game_schema.pickaxe_slots;
DROP TRIGGER IF EXISTS trg_user_ad_counters_updated ON game_schema.user_ad_counters;
DROP TRIGGER IF EXISTS trg_user_mission_daily_updated ON game_schema.user_mission_daily;
DROP TRIGGER IF EXISTS trg_user_mission_slots_updated ON game_schema.user_mission_slots;
DROP TRIGGER IF EXISTS trg_user_mission_weekly_updated ON game_schema.user_mission_weekly;
DROP TRIGGER IF EXISTS trg_user_infinite_mine_progress_updated ON game_schema.user_infinite_mine_progress;
DROP TRIGGER IF EXISTS trg_user_achievement_counters_updated ON game_schema.user_achievement_counters;
DROP TRIGGER IF EXISTS trg_user_achievement_chains_updated ON game_schema.user_achievement_chains;
DROP TRIGGER IF EXISTS trg_user_mail_updated ON game_schema.user_mail;
DROP TRIGGER IF EXISTS trg_user_item_inventory_updated ON game_schema.user_item_inventory;
DROP TRIGGER IF EXISTS trg_user_items_updated ON game_schema.user_items;
DROP TRIGGER IF EXISTS trg_user_item_instances_updated ON game_schema.user_item_instances;
DROP TRIGGER IF EXISTS trg_user_gem_inventory_updated ON game_schema.user_gem_inventory;
DROP TRIGGER IF EXISTS trg_user_gems_updated ON game_schema.user_gems;
DROP TRIGGER IF EXISTS trg_pickaxe_gem_slots_updated ON game_schema.pickaxe_gem_slots;
DROP TRIGGER IF EXISTS trg_pickaxe_equipped_gems_updated ON game_schema.pickaxe_equipped_gems;
DROP FUNCTION IF EXISTS game_schema.touch_updated_at;

-- Drop existing tables (order matters because of FK/PK relations)
DROP TABLE IF EXISTS game_schema.pickaxe_equipped_gems;
DROP TABLE IF EXISTS game_schema.pickaxe_gem_slots;
DROP TABLE IF EXISTS game_schema.user_gems;
DROP TABLE IF EXISTS game_schema.user_item_instances;
DROP TABLE IF EXISTS game_schema.user_items;
DROP TABLE IF EXISTS game_schema.user_item_inventory;
DROP TABLE IF EXISTS game_schema.user_gem_inventory;
DROP TABLE IF EXISTS game_schema.user_milestones;
DROP TABLE IF EXISTS game_schema.user_mail_rewards;
DROP TABLE IF EXISTS game_schema.user_mail;
DROP TABLE IF EXISTS game_schema.user_weekly_milestones;
DROP TABLE IF EXISTS game_schema.user_infinite_mine_progress;
DROP TABLE IF EXISTS game_schema.user_offline_state;
DROP TABLE IF EXISTS game_schema.user_achievement_chains;
DROP TABLE IF EXISTS game_schema.user_achievement_counters;
DROP TABLE IF EXISTS game_schema.user_mission_slots;
DROP TABLE IF EXISTS game_schema.user_mission_daily;
DROP TABLE IF EXISTS game_schema.user_mission_weekly;
DROP TABLE IF EXISTS game_schema.user_ad_counters;
DROP TABLE IF EXISTS game_schema.pickaxe_slots;
DROP TABLE IF EXISTS game_schema.user_game_data;

CREATE SCHEMA IF NOT EXISTS game_schema;

-- user core data (persistent)
CREATE TABLE IF NOT EXISTS game_schema.user_game_data (
    user_id               UUID PRIMARY KEY,
    gold                  BIGINT NOT NULL DEFAULT 0 CHECK (gold >= 0),
    crystal               INTEGER NOT NULL DEFAULT 0 CHECK (crystal >= 0),
    total_mining_count    BIGINT NOT NULL DEFAULT 0,
    highest_pickaxe_level INTEGER NOT NULL DEFAULT 0,
    unlocked_slots        BOOLEAN[4] NOT NULL DEFAULT ARRAY[TRUE, FALSE, FALSE, FALSE],
    total_dps             BIGINT NOT NULL DEFAULT 10 CHECK (total_dps >= 0),
    current_mineral_id    INTEGER NOT NULL DEFAULT 0,
    current_mineral_hp    BIGINT NOT NULL DEFAULT 0,
    cheat_score           INTEGER NOT NULL DEFAULT 0,
    created_at            TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at            TIMESTAMP NOT NULL DEFAULT NOW(),
    last_login_at         TIMESTAMP NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_user_game_gold ON game_schema.user_game_data(gold DESC);
CREATE INDEX IF NOT EXISTS idx_user_game_level ON game_schema.user_game_data(highest_pickaxe_level DESC);

-- pickaxe slots (persistent)
CREATE TABLE IF NOT EXISTS game_schema.pickaxe_slots (
    slot_id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id           UUID NOT NULL,
    slot_index        INTEGER NOT NULL CHECK (slot_index BETWEEN 0 AND 3),
    level             INTEGER NOT NULL DEFAULT 0 CHECK (level >= 0 AND level <= 109),
    tier              INTEGER NOT NULL DEFAULT 1 CHECK (tier BETWEEN 1 AND 22),
    attack_power      BIGINT NOT NULL DEFAULT 10 CHECK (attack_power > 0),
    attack_speed      INTEGER NOT NULL DEFAULT 10000 CHECK (attack_speed BETWEEN 10000 AND 250000),
    critical_hit_percent INTEGER NOT NULL DEFAULT 500 CHECK (critical_hit_percent BETWEEN 0 AND 10000),
    critical_damage   INTEGER NOT NULL DEFAULT 15000 CHECK (critical_damage >= 0),
    dps               BIGINT NOT NULL DEFAULT 10 CHECK (dps > 0),
    pity_bonus        INTEGER NOT NULL DEFAULT 0 CHECK (pity_bonus BETWEEN 0 AND 10000),
    created_at        TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at        TIMESTAMP NOT NULL DEFAULT NOW(),
    last_upgraded_at  TIMESTAMP,
    CONSTRAINT uq_user_slot UNIQUE (user_id, slot_index)
);
CREATE INDEX IF NOT EXISTS idx_pickaxe_user ON game_schema.pickaxe_slots(user_id);
CREATE INDEX IF NOT EXISTS idx_pickaxe_level ON game_schema.pickaxe_slots(level DESC);

-- ad counters (per day)
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

-- daily mission aggregate (per day)
CREATE TABLE IF NOT EXISTS game_schema.user_mission_daily (
    user_id         UUID NOT NULL,
    mission_date    DATE NOT NULL DEFAULT CURRENT_DATE,
    completed_count INTEGER NOT NULL DEFAULT 0 CHECK (completed_count >= 0),
    reroll_count    INTEGER NOT NULL DEFAULT 0 CHECK (reroll_count >= 0),
    created_at      TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_user_mission_daily PRIMARY KEY (user_id, mission_date)
);

-- mission slots (structured state)
CREATE TABLE IF NOT EXISTS game_schema.user_mission_slots (
    user_id         UUID NOT NULL,
    slot_no         INTEGER NOT NULL CHECK (slot_no BETWEEN 1 AND 3),
    mission_id      INTEGER NOT NULL CHECK (mission_id > 0),
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
CREATE TABLE IF NOT EXISTS game_schema.user_mission_weekly (
    user_id         UUID NOT NULL,
    week_start_date DATE NOT NULL,
    mission_id      INTEGER NOT NULL CHECK (mission_id > 0),
    mission_type    VARCHAR(50) NOT NULL,
    target_value    INTEGER NOT NULL CHECK (target_value > 0),
    current_value   INTEGER NOT NULL DEFAULT 0 CHECK (current_value >= 0),
    reward_crystal  INTEGER NOT NULL DEFAULT 0 CHECK (reward_crystal >= 0),
    status          VARCHAR(16) NOT NULL DEFAULT 'active' CHECK (status IN ('active', 'completed', 'claimed')),
    assigned_at     TIMESTAMP NOT NULL DEFAULT NOW(),
    completed_at    TIMESTAMP,
    claimed_at      TIMESTAMP,
    created_at      TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_user_mission_weekly PRIMARY KEY (user_id, week_start_date, mission_id)
);
CREATE INDEX IF NOT EXISTS idx_mission_weekly_status ON game_schema.user_mission_weekly(user_id, status);
CREATE INDEX IF NOT EXISTS idx_mission_weekly_week ON game_schema.user_mission_weekly(user_id, week_start_date);

CREATE TABLE IF NOT EXISTS game_schema.user_weekly_milestones (
    user_id         UUID NOT NULL,
    week_start_date DATE NOT NULL,
    milestone_count INTEGER NOT NULL CHECK (milestone_count > 0),
    claimed_at      TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_user_weekly_milestones PRIMARY KEY (user_id, week_start_date, milestone_count)
);
CREATE INDEX IF NOT EXISTS idx_user_weekly_milestones_week ON game_schema.user_weekly_milestones(user_id, week_start_date);

-- infinite mine progress (per floor)
CREATE TABLE IF NOT EXISTS game_schema.user_infinite_mine_progress (
    user_id              UUID NOT NULL,
    floor                INTEGER NOT NULL CHECK (floor > 0),
    first_cleared_at     TIMESTAMP NOT NULL DEFAULT NOW(),
    last_auto_claim_date DATE,
    created_at           TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at           TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_user_infinite_mine_progress PRIMARY KEY (user_id, floor)
);

-- 업적 누적 진행도
CREATE TABLE IF NOT EXISTS game_schema.user_achievement_counters (
    user_id           UUID NOT NULL,
    achievement_type  VARCHAR(50) NOT NULL,
    current_value     BIGINT NOT NULL DEFAULT 0 CHECK (current_value >= 0),
    created_at        TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at        TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_user_achievement_counters PRIMARY KEY (user_id, achievement_type)
);
CREATE INDEX IF NOT EXISTS idx_user_achievement_counters_user ON game_schema.user_achievement_counters(user_id);

-- 업적 체인 수령 상태
CREATE TABLE IF NOT EXISTS game_schema.user_achievement_chains (
    user_id            UUID NOT NULL,
    chain_id           INTEGER NOT NULL CHECK (chain_id > 0),
    last_claimed_step  INTEGER NOT NULL DEFAULT 0 CHECK (last_claimed_step >= 0),
    created_at         TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at         TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_user_achievement_chains PRIMARY KEY (user_id, chain_id)
);
CREATE INDEX IF NOT EXISTS idx_user_achievement_chains_user ON game_schema.user_achievement_chains(user_id);

-- offline state (per day)
CREATE TABLE IF NOT EXISTS game_schema.user_offline_state (
    user_id      UUID PRIMARY KEY,
    offline_date DATE NOT NULL DEFAULT CURRENT_DATE,
    -- seconds (hour*3600) 단위로 저장. 서버 로직과 동일한 단위 사용.
    current_offline_hours INTEGER NOT NULL DEFAULT 0 CHECK (current_offline_hours >= 0),
    updated_at   TIMESTAMP NOT NULL DEFAULT NOW()
);

-- milestone claims (per day)
CREATE TABLE IF NOT EXISTS game_schema.user_milestones (
    user_id         UUID NOT NULL,
    milestone_date  DATE NOT NULL DEFAULT CURRENT_DATE,
    milestone_count INTEGER NOT NULL CHECK (milestone_count IN (3, 5, 7)),
    claimed_at      TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_user_milestones PRIMARY KEY (user_id, milestone_date, milestone_count)
);

-- ========================================
-- 우편함 시스템 테이블
-- ========================================

CREATE TABLE IF NOT EXISTS game_schema.user_mail (
    mail_id      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id      UUID NOT NULL,
    mail_type    VARCHAR(32) NOT NULL DEFAULT 'system',
    template_id  VARCHAR(64) NOT NULL,
    template_args JSONB,
    title        VARCHAR(100),
    body         TEXT,
    sender       VARCHAR(64),
    source_type  VARCHAR(32),
    source_key   VARCHAR(64),
    created_at   TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at   TIMESTAMP NOT NULL DEFAULT NOW(),
    read_at      TIMESTAMP,
    claimed_at   TIMESTAMP,
    deleted_at   TIMESTAMP,
    expires_at   TIMESTAMP,
    CONSTRAINT uq_user_mail_source UNIQUE (user_id, source_type, source_key),
    CONSTRAINT chk_user_mail_source_pair CHECK (
        (source_type IS NULL AND source_key IS NULL)
        OR (source_type IS NOT NULL AND source_key IS NOT NULL)
    )
);
CREATE INDEX IF NOT EXISTS idx_user_mail_user_created ON game_schema.user_mail(user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_user_mail_user_claimed ON game_schema.user_mail(user_id, claimed_at);
CREATE INDEX IF NOT EXISTS idx_user_mail_expires ON game_schema.user_mail(expires_at);

CREATE TABLE IF NOT EXISTS game_schema.user_mail_rewards (
    mail_id       UUID NOT NULL,
    reward_index  INTEGER NOT NULL CHECK (reward_index >= 0),
    reward_type   VARCHAR(16) NOT NULL CHECK (reward_type IN ('gold', 'crystal', 'item')),
    reward_key    VARCHAR(64),
    amount        BIGINT NOT NULL CHECK (amount >= 0),
    created_at    TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_user_mail_rewards PRIMARY KEY (mail_id, reward_index),
    CONSTRAINT fk_user_mail_rewards_mail FOREIGN KEY (mail_id)
        REFERENCES game_schema.user_mail(mail_id) ON DELETE CASCADE
);

-- ========================================
-- Item Inventory Tables
-- ========================================

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

-- ========================================
-- Gem System Tables
-- ========================================

-- 유저 보석 인벤토리 용량
CREATE TABLE IF NOT EXISTS game_schema.user_gem_inventory (
    user_id           UUID PRIMARY KEY,
    current_capacity  INTEGER NOT NULL DEFAULT 48 CHECK (current_capacity >= 0),
    created_at        TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at        TIMESTAMP NOT NULL DEFAULT NOW()
);

-- 유저 보유 보석 (인스턴스)
CREATE TABLE IF NOT EXISTS game_schema.user_gems (
    gem_instance_id   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id           UUID NOT NULL,
    gem_id            INTEGER NOT NULL CHECK (gem_id >= 0),
    acquired_at       TIMESTAMP NOT NULL DEFAULT NOW(),
    created_at        TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at        TIMESTAMP NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_user_gems_user ON game_schema.user_gems(user_id);
CREATE INDEX IF NOT EXISTS idx_user_gems_gem_id ON game_schema.user_gems(gem_id);

-- 곡괭이별 보석 슬롯 해금 상태
CREATE TABLE IF NOT EXISTS game_schema.pickaxe_gem_slots (
    pickaxe_slot_id   UUID NOT NULL,
    gem_slot_index    INTEGER NOT NULL CHECK (gem_slot_index BETWEEN 0 AND 5),
    is_unlocked       BOOLEAN NOT NULL DEFAULT FALSE,
    unlocked_at       TIMESTAMP,
    created_at        TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at        TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_pickaxe_gem_slots PRIMARY KEY (pickaxe_slot_id, gem_slot_index),
    CONSTRAINT fk_pickaxe_gem_slots_pickaxe FOREIGN KEY (pickaxe_slot_id)
        REFERENCES game_schema.pickaxe_slots(slot_id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_pickaxe_gem_slots_pickaxe ON game_schema.pickaxe_gem_slots(pickaxe_slot_id);

-- 곡괭이에 장착된 보석
CREATE TABLE IF NOT EXISTS game_schema.pickaxe_equipped_gems (
    pickaxe_slot_id   UUID NOT NULL,
    gem_slot_index    INTEGER NOT NULL CHECK (gem_slot_index BETWEEN 0 AND 5),
    gem_instance_id   UUID NOT NULL,
    equipped_at       TIMESTAMP NOT NULL DEFAULT NOW(),
    created_at        TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at        TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_pickaxe_equipped_gems PRIMARY KEY (pickaxe_slot_id, gem_slot_index),
    CONSTRAINT fk_equipped_gems_gem_slot FOREIGN KEY (pickaxe_slot_id, gem_slot_index)
        REFERENCES game_schema.pickaxe_gem_slots(pickaxe_slot_id, gem_slot_index) ON DELETE CASCADE,
    CONSTRAINT fk_equipped_gems_instance FOREIGN KEY (gem_instance_id)
        REFERENCES game_schema.user_gems(gem_instance_id) ON DELETE CASCADE,
    CONSTRAINT uq_equipped_gem_instance UNIQUE (gem_instance_id)
);
CREATE INDEX IF NOT EXISTS idx_equipped_gems_pickaxe ON game_schema.pickaxe_equipped_gems(pickaxe_slot_id);
CREATE INDEX IF NOT EXISTS idx_equipped_gems_instance ON game_schema.pickaxe_equipped_gems(gem_instance_id);

-- ========================================

-- updated_at auto-touch trigger
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

CREATE TRIGGER trg_user_mission_weekly_updated
    BEFORE UPDATE ON game_schema.user_mission_weekly
    FOR EACH ROW EXECUTE FUNCTION game_schema.touch_updated_at();

CREATE TRIGGER trg_user_infinite_mine_progress_updated
    BEFORE UPDATE ON game_schema.user_infinite_mine_progress
    FOR EACH ROW EXECUTE FUNCTION game_schema.touch_updated_at();

CREATE TRIGGER trg_user_achievement_counters_updated
    BEFORE UPDATE ON game_schema.user_achievement_counters
    FOR EACH ROW EXECUTE FUNCTION game_schema.touch_updated_at();

CREATE TRIGGER trg_user_achievement_chains_updated
    BEFORE UPDATE ON game_schema.user_achievement_chains
    FOR EACH ROW EXECUTE FUNCTION game_schema.touch_updated_at();

CREATE TRIGGER trg_user_mail_updated
    BEFORE UPDATE ON game_schema.user_mail
    FOR EACH ROW EXECUTE FUNCTION game_schema.touch_updated_at();

CREATE TRIGGER trg_user_item_inventory_updated
    BEFORE UPDATE ON game_schema.user_item_inventory
    FOR EACH ROW EXECUTE FUNCTION game_schema.touch_updated_at();

CREATE TRIGGER trg_user_items_updated
    BEFORE UPDATE ON game_schema.user_items
    FOR EACH ROW EXECUTE FUNCTION game_schema.touch_updated_at();

CREATE TRIGGER trg_user_item_instances_updated
    BEFORE UPDATE ON game_schema.user_item_instances
    FOR EACH ROW EXECUTE FUNCTION game_schema.touch_updated_at();

CREATE TRIGGER trg_user_gem_inventory_updated
    BEFORE UPDATE ON game_schema.user_gem_inventory
    FOR EACH ROW EXECUTE FUNCTION game_schema.touch_updated_at();

CREATE TRIGGER trg_user_gems_updated
    BEFORE UPDATE ON game_schema.user_gems
    FOR EACH ROW EXECUTE FUNCTION game_schema.touch_updated_at();

CREATE TRIGGER trg_pickaxe_gem_slots_updated
    BEFORE UPDATE ON game_schema.pickaxe_gem_slots
    FOR EACH ROW EXECUTE FUNCTION game_schema.touch_updated_at();

CREATE TRIGGER trg_pickaxe_equipped_gems_updated
    BEFORE UPDATE ON game_schema.pickaxe_equipped_gems
    FOR EACH ROW EXECUTE FUNCTION game_schema.touch_updated_at();
