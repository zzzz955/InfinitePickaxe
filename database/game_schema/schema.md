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
| total_dps | BIGINT | DEFAULT 10, CHECK >=0 | DPS 캐시 |
| current_mineral_id | INTEGER | DEFAULT 0 | 현재 광물 ID(0=미선택) |
| current_mineral_hp | BIGINT | DEFAULT 0 | 현재 광물 HP |
| max_offline_hours | INTEGER | 삭제 | (스키마에서 제거됨) |
| cheat_score | INTEGER | DEFAULT 0 | 치트 탐지 |
| created_at | TIMESTAMP | DEFAULT now | 생성 |
| updated_at | TIMESTAMP | DEFAULT now | 업데이트 |
| last_login_at | TIMESTAMP | DEFAULT now | 최근 로그인 |

## pickaxe_slots
| 컬럼 | 타입 | 제약/기본값 | 비고 |
| --- | --- | --- | --- |
| slot_id | UUID | PK |  |
| user_id | UUID |  | 논리 FK |
| slot_index | INTEGER | CHECK 0-3, UNIQUE per user | 슬롯 번호 |
| level | INTEGER | DEFAULT 0, CHECK 0-109 | 레벨 |
| tier | INTEGER | DEFAULT 1, CHECK 1-22 | 티어 |
| attack_power | BIGINT | DEFAULT 10, CHECK >0 | 공격력 |
| attack_speed_x100 | INTEGER | DEFAULT 100, CHECK 100-2500 | 100=1회/s, 2500=25회/s |
| critical_hit_percent | INTEGER | DEFAULT 500, CHECK 0-10000 | 크리확(x100) |
| critical_damage | INTEGER | DEFAULT 15000, CHECK >=0 | 크리뎀(x100, 15000=150%) |
| dps | BIGINT | DEFAULT 10, CHECK >0 | DPS |
| pity_bonus | INTEGER | DEFAULT 0, CHECK 0-10000 | 실패 보정 (basis 10000=100.00%) |
| created_at | TIMESTAMP | DEFAULT now |  |
| updated_at | TIMESTAMP | DEFAULT now |  |
| last_upgraded_at | TIMESTAMP |  | 최근 강화 |

## user_ad_counters
| 컬럼 | 타입 | 제약/기본값 | 비고 |
| --- | --- | --- | --- |
| user_id | UUID | PK part |  |
| ad_type | VARCHAR(32) | PK part, CHECK IN (upgrade_discount, mission_reroll, crystal_reward) | 광고 타입 |
| ad_count | INTEGER | DEFAULT 0, CHECK >=0 | 일일 시청 횟수 |
| reset_date | DATE | DEFAULT CURRENT_DATE | 리셋 기준일 |
| created_at | TIMESTAMP | DEFAULT now |  |
| updated_at | TIMESTAMP | DEFAULT now |  |

## user_mission_daily
| 컬럼 | 타입 | 제약/기본값 | 비고 |
| --- | --- | --- | --- |
| user_id | UUID | PK part |  |
| mission_date | DATE | PK part, DEFAULT CURRENT_DATE | 기준 일자 |
| assigned_count | 삭제 |  |  |
| reroll_count | INTEGER | DEFAULT 0, CHECK >=0 | 리롤 횟수 |
| created_at | TIMESTAMP | DEFAULT now |  |
| updated_at | TIMESTAMP | DEFAULT now |  |

## user_mission_slots
| 컬럼 | 타입 | 제약/기본값 | 비고 |
| --- | --- | --- | --- |
| user_id | UUID | PK part |  |
| slot_no | INTEGER | PK part, CHECK 1-3 | 활성 슬롯 3개 |
| mission_id | INTEGER | UNIQUE per user, CHECK >0 | 메타 미션 ID |
| mission_type | VARCHAR(50) | NOT NULL | 미션 타입 |
| target_value | INTEGER | NOT NULL, >0 | 목표 |
| current_value | INTEGER | DEFAULT 0, CHECK >=0 | 진행 |
| reward_crystal | INTEGER | DEFAULT 0, CHECK >=0 | 보상 |
| status | VARCHAR(16) | DEFAULT 'active', CHECK IN (active, completed, claimed) | 상태 |
| assigned_at | TIMESTAMP | DEFAULT now | 배정 |
| completed_at | TIMESTAMP |  | 완료 |
| claimed_at | TIMESTAMP |  | 수령 |
| expires_at | TIMESTAMP |  | 만료 |
| created_at | TIMESTAMP | DEFAULT now |  |
| updated_at | TIMESTAMP | DEFAULT now |  |

## 트리거
- updated_at 자동 갱신: user_game_data, pickaxe_slots, user_ad_counters, user_mission_daily, user_mission_slots 테이블에 BEFORE UPDATE 트리거 적용.
