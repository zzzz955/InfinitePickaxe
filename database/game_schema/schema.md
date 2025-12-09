# game_schema 테이블 요약 (MVP)

## user_game_data
| 컬럼 | 타입 | 제약/기본값 | 비고 |
| --- | --- | --- | --- |
| user_id | UUID | PK | auth.users 논리 참조 |
| gold | BIGINT | DEFAULT 0, CHECK >=0 | 재화 |
| crystal | INTEGER | DEFAULT 0, CHECK >=0 | 재화 |
| total_mining_count | BIGINT | DEFAULT 0 | 누적 채굴 |
| highest_pickaxe_level | INTEGER | DEFAULT 0 | 최고 레벨 |
| unlocked_slots | BOOLEAN[4] | DEFAULT [T,F,F,F] | 슬롯 해금 |
| ad_count_today | INTEGER | DEFAULT 0 | 일일 광고 |
| ad_reset_date | DATE | DEFAULT CURRENT_DATE | Lazy reset(KST) |
| mission_reroll_free | INTEGER | DEFAULT 2 | 무료 리롤 |
| mission_reroll_ad | INTEGER | DEFAULT 3 | 광고 리롤 |
| mission_reset_date | DATE | DEFAULT CURRENT_DATE | Lazy reset(KST) |
| max_offline_hours | INTEGER | DEFAULT 3 | 오프라인 최대 |
| cheat_score | INTEGER | DEFAULT 0 | 치트 탐지 |
| created_at | TIMESTAMP | DEFAULT now | 생성 |
| updated_at | TIMESTAMP | DEFAULT now | 업데이트 |

## pickaxe_slots
| 컬럼 | 타입 | 제약/기본값 | 비고 |
| --- | --- | --- | --- |
| slot_id | UUID | PK |  |
| user_id | UUID |  | 논리 FK |
| slot_index | INTEGER | CHECK 0-3, UNIQUE per user | 슬롯 번호 |
| level | INTEGER | DEFAULT 0, CHECK 0-100 | 레벨 |
| tier | INTEGER | DEFAULT 1, CHECK 1-5 | 티어 |
| dps | BIGINT | DEFAULT 10, CHECK >0 | DPS |
| pity_bonus | INTEGER | DEFAULT 0, CHECK 0-10000 | 실패 보정 (basis 10000=100.00%) |
| created_at | TIMESTAMP | DEFAULT now |  |
| updated_at | TIMESTAMP | DEFAULT now |  |
| last_upgraded_at | TIMESTAMP |  | 최근 강화 |

## mining_snapshots
| 컬럼 | 타입 | 제약/기본값 | 비고 |
| --- | --- | --- | --- |
| snapshot_id | UUID | PK |  |
| user_id | UUID |  | 논리 FK |
| mineral_id | INTEGER | CHECK 0-6 | 광물 ID |
| current_hp | BIGINT | CHECK >=0 | 진행 HP |
| max_hp | BIGINT | NOT NULL | 최대 HP |
| mining_start_time | TIMESTAMP | NOT NULL | 시작 시각 |
| snapshot_time | TIMESTAMP | DEFAULT now | 스냅샷 시각 |
| uq_user_mineral | 제약 | UNIQUE (user_id, mineral_id) | 유저별 1개 |

## mining_completions
| 컬럼 | 타입 | 제약/기본값 | 비고 |
| --- | --- | --- | --- |
| completion_id | UUID | PK |  |
| user_id | UUID |  | 논리 FK |
| mineral_id | INTEGER | NOT NULL | 광물 |
| gold_earned | BIGINT | CHECK >=0 | 획득 골드 |
| mining_duration_seconds | INTEGER | NOT NULL | 소요 시간 |
| completed_at | TIMESTAMP | DEFAULT now | 완료 시각 |

## daily_missions
| 컬럼 | 타입 | 제약/기본값 | 비고 |
| --- | --- | --- | --- |
| mission_id | UUID | PK |  |
| user_id | UUID |  | 논리 FK |
| mission_index | INTEGER | CHECK 0-6, UNIQUE per user/reset_at | 슬롯 |
| mission_type | VARCHAR(50) | NOT NULL | MINE_COUNT 등 |
| target_value | INTEGER | NOT NULL | 목표 |
| current_value | INTEGER | DEFAULT 0 | 진행 |
| reward_crystal | INTEGER | NOT NULL | 보상 |
| is_completed | BOOLEAN | DEFAULT false | 완료 여부 |
| is_claimed | BOOLEAN | DEFAULT false | 수령 여부 |
| assigned_at | TIMESTAMP | DEFAULT now | 배정 |
| completed_at | TIMESTAMP |  | 완료 |
| claimed_at | TIMESTAMP |  | 수령 |
| reset_at | TIMESTAMP | NOT NULL | KST 00:00 |

## critical_transactions
| 컬럼 | 타입 | 제약/기본값 | 비고 |
| --- | --- | --- | --- |
| transaction_id | UUID | PK |  |
| user_id | UUID |  | 논리 FK |
| transaction_type | VARCHAR(50) | CHECK IN (...) | IAP/SLOT_UNLOCK/REFUND/ADMIN_ADJUST |
| gold_delta | BIGINT | DEFAULT 0 | 골드 변화 |
| crystal_delta | INTEGER | DEFAULT 0 | 크리스탈 변화 |
| gold_after | BIGINT | NOT NULL | 거래 후 골드 |
| crystal_after | INTEGER | NOT NULL | 거래 후 크리스탈 |
| metadata | JSONB |  | 추가 정보 |
| created_at | TIMESTAMP | DEFAULT now | 생성 |
