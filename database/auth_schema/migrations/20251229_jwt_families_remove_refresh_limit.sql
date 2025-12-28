BEGIN;

ALTER TABLE auth_schema.jwt_families
  DROP CONSTRAINT IF EXISTS chk_refresh_limit;

ALTER TABLE auth_schema.jwt_families
  DROP COLUMN IF EXISTS refresh_count,
  DROP COLUMN IF EXISTS max_refresh_count;

COMMIT;
