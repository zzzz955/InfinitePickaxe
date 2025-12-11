-- Migration: remove users.device_id, redefine session_history for auth usage

BEGIN;

-- Remove device_id from users (no longer stored here)
ALTER TABLE auth_schema.users
  DROP COLUMN IF EXISTS device_id;

-- Redefine session_history structure
DROP TABLE IF EXISTS auth_schema.session_history;

CREATE TABLE auth_schema.session_history (
    session_id       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id          UUID NOT NULL REFERENCES auth_schema.users(user_id) ON DELETE CASCADE,
    provider         VARCHAR(30),
    external_id      VARCHAR(255),
    device_id        VARCHAR(255),
    client_ip        INET,
    user_agent       TEXT,
    result           VARCHAR(20),
    reason           TEXT,
    login_at         TIMESTAMP NOT NULL DEFAULT NOW(),
    logout_at        TIMESTAMP,
    duration_seconds INTEGER,
    created_at       TIMESTAMP NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_session_history_user ON auth_schema.session_history(user_id);
CREATE INDEX IF NOT EXISTS idx_session_history_login ON auth_schema.session_history(login_at DESC);
CREATE INDEX IF NOT EXISTS idx_session_history_external ON auth_schema.session_history(external_id);

COMMIT;
