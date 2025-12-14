-- Migration: Add attack_power and attack_speed_x100 to pickaxe_slots
-- Date: 2024-12-14
-- Description: DPS를 공격력과 공격속도로 분리

-- 1. 컬럼 추가 (기본값으로 초기화)
ALTER TABLE game_schema.pickaxe_slots
ADD COLUMN IF NOT EXISTS attack_power BIGINT NOT NULL DEFAULT 10 CHECK (attack_power > 0);

ALTER TABLE game_schema.pickaxe_slots
ADD COLUMN IF NOT EXISTS attack_speed_x100 INTEGER NOT NULL DEFAULT 100 CHECK (attack_speed_x100 > 0);

-- 2. 기존 데이터 마이그레이션
-- MVP에서는 attack_speed가 1.0 고정이므로:
-- attack_power = dps
-- attack_speed_x100 = 100 (1.0 APS)
UPDATE game_schema.pickaxe_slots
SET
    attack_power = dps,
    attack_speed_x100 = 100
WHERE attack_power = 10 AND attack_speed_x100 = 100;  -- 기본값인 경우만 업데이트

-- 3. 검증: DPS가 일치하는지 확인
DO $$
DECLARE
    mismatch_count INTEGER;
BEGIN
    SELECT COUNT(*) INTO mismatch_count
    FROM game_schema.pickaxe_slots
    WHERE dps != (attack_power * attack_speed_x100 / 100);

    IF mismatch_count > 0 THEN
        RAISE WARNING 'DPS 불일치 발견: % 건', mismatch_count;
    ELSE
        RAISE NOTICE '마이그레이션 완료: 모든 DPS가 일치합니다';
    END IF;
END $$;
