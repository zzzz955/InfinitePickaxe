CREATE SCHEMA IF NOT EXISTS game_schema;

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

DROP TRIGGER IF EXISTS trg_user_mail_updated ON game_schema.user_mail;
CREATE TRIGGER trg_user_mail_updated
    BEFORE UPDATE ON game_schema.user_mail
    FOR EACH ROW EXECUTE FUNCTION game_schema.touch_updated_at();
