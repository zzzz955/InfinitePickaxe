-- ====================================================================
-- Migration: Add current_mineral_id and current_mineral_hp to user_game_data
-- Date: 2024-12-14
-- Description:
--   - 현재 채굴 중인 광물 정보를 user_game_data에 추가
--   - mining_snapshots 테이블 대체
-- ====================================================================

BEGIN;

-- 1. user_game_data에 current_mineral_id, current_mineral_hp 컬럼 추가
ALTER TABLE game_schema.user_game_data
ADD COLUMN IF NOT EXISTS current_mineral_id INTEGER NOT NULL DEFAULT 1 CHECK (current_mineral_id BETWEEN 1 AND 7),
ADD COLUMN IF NOT EXISTS current_mineral_hp BIGINT NOT NULL DEFAULT 100 CHECK (current_mineral_hp >= 0);

-- 2. 기존 데이터 마이그레이션 (mining_snapshots에서 데이터가 있다면)
-- mining_snapshots에서 가장 최근 스냅샷 데이터를 user_game_data로 이동
UPDATE game_schema.user_game_data u
SET
    current_mineral_id = COALESCE(s.mineral_id, 1),
    current_mineral_hp = COALESCE(s.current_hp, 100)
FROM (
    SELECT DISTINCT ON (user_id)
        user_id,
        mineral_id,
        current_hp
    FROM game_schema.mining_snapshots
    ORDER BY user_id, snapshot_time DESC
) s
WHERE u.user_id = s.user_id
  AND EXISTS (SELECT 1 FROM game_schema.mining_snapshots WHERE user_id = u.user_id);

-- 3. 검증: current_mineral_id와 current_mineral_hp가 유효한 범위인지 확인
DO $$
DECLARE
    invalid_count INTEGER;
BEGIN
    SELECT COUNT(*) INTO invalid_count
    FROM game_schema.user_game_data
    WHERE current_mineral_id < 1 OR current_mineral_id > 7
       OR current_mineral_hp < 0;

    IF invalid_count > 0 THEN
        RAISE EXCEPTION 'Migration failed: % invalid current_mineral values found', invalid_count;
    END IF;

    RAISE NOTICE 'Migration successful: All current_mineral values are valid';
END $$;

COMMIT;
