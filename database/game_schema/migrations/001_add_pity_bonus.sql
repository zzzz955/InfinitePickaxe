ALTER TABLE game_schema.pickaxe_slots
    ADD COLUMN IF NOT EXISTS pity_bonus INTEGER NOT NULL DEFAULT 0 CHECK (pity_bonus >= 0 AND pity_bonus <= 10000);

COMMENT ON COLUMN game_schema.pickaxe_slots.pity_bonus IS '강화 실패 누적 보너스 (basis 10000 = 100.00%)';
