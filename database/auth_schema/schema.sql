-- Auth schema DDL (MVP)
-- Requires: CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE SCHEMA IF NOT EXISTS auth_schema;

CREATE TABLE IF NOT EXISTS auth_schema.users (
    user_id       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    provider      VARCHAR(30) NOT NULL DEFAULT 'google',
    external_id   VARCHAR(255) NOT NULL UNIQUE,
    nickname      VARCHAR(50),
    email         VARCHAR(255),
    created_at    TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at    TIMESTAMP NOT NULL DEFAULT NOW(),
    last_login    TIMESTAMP,
    last_logout   TIMESTAMP,
    is_banned     BOOLEAN NOT NULL DEFAULT FALSE,
    ban_reason    TEXT,
    banned_at     TIMESTAMP,
    banned_until  TIMESTAMP  -- NULL이면 영구 밴
);
CREATE INDEX IF NOT EXISTS idx_auth_users_last_login ON auth_schema.users(last_login DESC);
CREATE INDEX IF NOT EXISTS idx_auth_users_banned ON auth_schema.users(is_banned) WHERE is_banned = TRUE;
CREATE INDEX IF NOT EXISTS idx_auth_users_email ON auth_schema.users(email);

CREATE TABLE IF NOT EXISTS auth_schema.jwt_families (
    family_id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id            UUID NOT NULL REFERENCES auth_schema.users(user_id) ON DELETE CASCADE,
    device_id          VARCHAR(255),
    login_ip           INET,
    user_agent         TEXT,
    is_active          BOOLEAN NOT NULL DEFAULT TRUE,
    is_revoked         BOOLEAN NOT NULL DEFAULT FALSE,
    revoked_reason     TEXT,
    created_at         TIMESTAMP NOT NULL DEFAULT NOW(),
    last_refreshed_at  TIMESTAMP NOT NULL DEFAULT NOW(),
    expires_at         TIMESTAMP NOT NULL,
    revoked_at         TIMESTAMP,
    refresh_count      INTEGER NOT NULL DEFAULT 0,
    max_refresh_count  INTEGER NOT NULL DEFAULT 100,
    CONSTRAINT chk_refresh_limit CHECK (refresh_count <= max_refresh_count)
);
CREATE INDEX IF NOT EXISTS idx_jwt_families_user ON auth_schema.jwt_families(user_id);
CREATE INDEX IF NOT EXISTS idx_jwt_families_active ON auth_schema.jwt_families(user_id, is_active) WHERE is_active = TRUE;
CREATE INDEX IF NOT EXISTS idx_jwt_families_expires ON auth_schema.jwt_families(expires_at);

CREATE TABLE IF NOT EXISTS auth_schema.jwt_tokens (
    token_id    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    family_id   UUID NOT NULL REFERENCES auth_schema.jwt_families(family_id) ON DELETE CASCADE,
    user_id     UUID NOT NULL REFERENCES auth_schema.users(user_id) ON DELETE CASCADE,
    token_hash  VARCHAR(64) NOT NULL UNIQUE,  -- SHA-256
    jti         VARCHAR(36) NOT NULL UNIQUE,  -- JWT ID
    is_valid    BOOLEAN NOT NULL DEFAULT TRUE,
    is_used     BOOLEAN NOT NULL DEFAULT FALSE,
    issued_at   TIMESTAMP NOT NULL DEFAULT NOW(),
    expires_at  TIMESTAMP NOT NULL,
    used_at     TIMESTAMP,
    revoked_at  TIMESTAMP,
    issued_ip   INET,
    used_ip     INET
);
CREATE INDEX IF NOT EXISTS idx_jwt_tokens_family ON auth_schema.jwt_tokens(family_id);
CREATE INDEX IF NOT EXISTS idx_jwt_tokens_valid ON auth_schema.jwt_tokens(family_id, is_valid) WHERE is_valid = TRUE;

CREATE TABLE IF NOT EXISTS auth_schema.session_history (
    session_id       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id          UUID NOT NULL REFERENCES auth_schema.users(user_id) ON DELETE CASCADE,
    provider         VARCHAR(30),
    external_id      VARCHAR(255),
    device_id        VARCHAR(255),
    client_ip        INET,
    user_agent       TEXT,
    result           VARCHAR(20),  -- SUCCESS / FAIL / BANNED / INVALID
    reason           TEXT,
    login_at         TIMESTAMP NOT NULL DEFAULT NOW(),
    logout_at        TIMESTAMP,
    duration_seconds INTEGER,
    created_at       TIMESTAMP NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_session_history_user ON auth_schema.session_history(user_id);
CREATE INDEX IF NOT EXISTS idx_session_history_login ON auth_schema.session_history(login_at DESC);
CREATE INDEX IF NOT EXISTS idx_session_history_external ON auth_schema.session_history(external_id);

CREATE TABLE IF NOT EXISTS auth_schema.ban_history (
    ban_id       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id      UUID NOT NULL REFERENCES auth_schema.users(user_id) ON DELETE CASCADE,
    ban_reason   TEXT NOT NULL,
    ban_type     VARCHAR(20) NOT NULL,  -- TEMPORARY, PERMANENT
    banned_by    VARCHAR(100) NOT NULL DEFAULT 'SYSTEM',
    banned_at    TIMESTAMP NOT NULL DEFAULT NOW(),
    banned_until TIMESTAMP,
    unbanned_at  TIMESTAMP,
    unban_reason TEXT
);
CREATE INDEX IF NOT EXISTS idx_ban_history_user ON auth_schema.ban_history(user_id);
CREATE INDEX IF NOT EXISTS idx_ban_history_time ON auth_schema.ban_history(banned_at DESC);
