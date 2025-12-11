-- Migration: introduce provider/external_id, drop google_id/display_name, add profile fields
-- Run order: apply on auth_schema

BEGIN;

-- New columns
ALTER TABLE auth_schema.users
  ADD COLUMN IF NOT EXISTS provider VARCHAR(30) NOT NULL DEFAULT 'google',
  ADD COLUMN IF NOT EXISTS external_id VARCHAR(255);

-- Add profile fields if missing
ALTER TABLE auth_schema.users
  ADD COLUMN IF NOT EXISTS nickname VARCHAR(50),
  ADD COLUMN IF NOT EXISTS email VARCHAR(255);

-- Enforce NOT NULL + uniqueness on external_id
ALTER TABLE auth_schema.users
  ALTER COLUMN external_id SET NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS idx_auth_users_external_id ON auth_schema.users(external_id);

-- Drop legacy columns
ALTER TABLE auth_schema.users
  DROP COLUMN IF EXISTS google_id,
  DROP COLUMN IF EXISTS display_name,
  DROP COLUMN IF EXISTS username;

-- Email index (non-unique)
CREATE INDEX IF NOT EXISTS idx_auth_users_email ON auth_schema.users(email);

COMMIT;
