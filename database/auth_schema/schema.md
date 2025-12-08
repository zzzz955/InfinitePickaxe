# auth_schema 테이블 요약 (MVP)

## users
| 컬럼 | 타입 | 제약/기본값 | 비고 |
| --- | --- | --- | --- |
| user_id | UUID | PK, `gen_random_uuid()` | 내부 PK |
| google_id | VARCHAR(255) | UNIQUE, NOT NULL | Google ID |
| username | VARCHAR(50) |  | 표시명(옵션) |
| device_id | VARCHAR(255) |  | 최근 디바이스 |
| created_at | TIMESTAMP | DEFAULT now | 생성 시각 |
| updated_at | TIMESTAMP | DEFAULT now | 업데이트 시각 |
| last_login | TIMESTAMP |  | 마지막 로그인 |
| last_logout | TIMESTAMP |  | 마지막 로그아웃 |
| is_banned | BOOLEAN | DEFAULT false | 밴 여부 |
| ban_reason | TEXT |  | 밴 사유 |
| banned_at | TIMESTAMP |  | 밴 시작 |
| banned_until | TIMESTAMP |  | NULL이면 영구 |

## jwt_families
| 컬럼 | 타입 | 제약/기본값 | 비고 |
| --- | --- | --- | --- |
| family_id | UUID | PK | 슬라이딩 세션 패밀리 |
| user_id | UUID | FK→users | 유저 |
| device_id | VARCHAR(255) |  | 디바이스 |
| login_ip | INET |  | 로그인 IP |
| user_agent | TEXT |  | UA |
| is_active | BOOLEAN | DEFAULT true | 활성 |
| is_revoked | BOOLEAN | DEFAULT false | 철회 여부 |
| revoked_reason | TEXT |  | 철회 사유 |
| created_at | TIMESTAMP | DEFAULT now | 생성 |
| last_refreshed_at | TIMESTAMP | DEFAULT now | 마지막 리프레시 |
| expires_at | TIMESTAMP | NOT NULL | 만료 |
| revoked_at | TIMESTAMP |  | 철회 시각 |
| refresh_count | INTEGER | DEFAULT 0 | 리프레시 횟수 |
| max_refresh_count | INTEGER | DEFAULT 100, CHECK | 최대 허용 |

## jwt_tokens
| 컬럼 | 타입 | 제약/기본값 | 비고 |
| --- | --- | --- | --- |
| token_id | UUID | PK |  |
| family_id | UUID | FK→jwt_families | 패밀리 |
| user_id | UUID | FK→users | 유저 |
| token_hash | VARCHAR(64) | UNIQUE, NOT NULL | SHA-256 |
| jti | VARCHAR(36) | UNIQUE, NOT NULL | JWT ID |
| is_valid | BOOLEAN | DEFAULT true | 유효 여부 |
| is_used | BOOLEAN | DEFAULT false | 리프레시에 사용됨 |
| issued_at | TIMESTAMP | DEFAULT now | 발급 |
| expires_at | TIMESTAMP | NOT NULL | 만료 |
| used_at | TIMESTAMP |  | 사용 시각 |
| revoked_at | TIMESTAMP |  | 철회 시각 |
| issued_ip | INET |  | 발급 IP |
| used_ip | INET |  | 사용 IP |

## session_history
| 컬럼 | 타입 | 제약/기본값 | 비고 |
| --- | --- | --- | --- |
| session_id | UUID | PK |  |
| user_id | UUID | FK→users | 유저 |
| server_ip | VARCHAR(45) |  | 서버 IP |
| client_ip | INET |  | 클라이언트 IP |
| client_version | VARCHAR(20) |  | 클라이언트 버전 |
| connected_at | TIMESTAMP | DEFAULT now | 접속 |
| disconnected_at | TIMESTAMP |  | 종료 |
| duration_seconds | INTEGER |  | 지속 |
| disconnect_reason | TEXT |  | 종료 이유 |
| packets_sent | BIGINT |  | 선택적 |
| packets_received | BIGINT |  | 선택적 |

## ban_history
| 컬럼 | 타입 | 제약/기본값 | 비고 |
| --- | --- | --- | --- |
| ban_id | UUID | PK |  |
| user_id | UUID | FK→users | 유저 |
| ban_reason | TEXT | NOT NULL | 밴 사유 |
| ban_type | VARCHAR(20) | NOT NULL | TEMPORARY/PERMANENT |
| banned_by | VARCHAR(100) | DEFAULT 'SYSTEM' | 관리 주체 |
| banned_at | TIMESTAMP | DEFAULT now | 밴 시각 |
| banned_until | TIMESTAMP |  | 종료 예정 |
| unbanned_at | TIMESTAMP |  | 해제 시각 |
| unban_reason | TEXT |  | 해제 사유 |
