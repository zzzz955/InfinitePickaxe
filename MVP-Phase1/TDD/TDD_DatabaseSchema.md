# 무한의 곡괭이: 데이터베이스 스키마 설계
## Technical Design Document - Database Schema

**버전**: 2.0 (MVP - 스키마 분리)  
**작성일**: 2024-12-08  
**문서 유형**: 기술 설계 문서 - 데이터베이스 스키마  

---

## 목차
1. [데이터베이스 개요](#1-데이터베이스-개요)
2. [아키텍처 전략](#2-아키텍처-전략)
3. [ERD (Entity Relationship Diagram)](#3-erd-entity-relationship-diagram)
4. [auth_schema (인증 스키마)](#4-auth_schema-인증-스키마)
5. [game_schema (게임 스키마)](#5-game_schema-게임-스키마)
6. [인덱스 전략](#6-인덱스-전략)
7. [트랜잭션 정책](#7-트랜잭션-정책)
8. [쿼리 예시](#8-쿼리-예시)
9. [권한 관리](#9-권한-관리)
10. [백업 및 복구](#10-백업-및-복구)
11. [성능 최적화](#11-성능-최적화)
12. [마이그레이션 전략](#12-마이그레이션-전략)

---

## 1. 데이터베이스 개요

### 1-1. 기본 정보

| 항목 | 내용 |
|------|------|
| **DBMS** | PostgreSQL 15.x |
| **인코딩** | UTF-8 |
| **타임존** | UTC |
| **포트** | 10002 (호스트), 5432 (컨테이너) |
| **데이터베이스명** | infinite_pickaxe |
| **스키마** | auth_schema, game_schema |

### 1-2. 설계 철학

**물리적 단일 DB + 논리적 스키마 분리**

```
┌────────────────────────────────────────┐
│     PostgreSQL (물리적 1개)            │
│                                        │
│  ┌──────────────┐  ┌───────────────┐  │
│  │ auth_schema  │  │ game_schema   │  │
│  │              │  │               │  │
│  │ - users      │  │ - user_game_  │  │
│  │ - jwt_*      │  │   data        │  │
│  │ - session_   │  │ - pickaxe_*   │  │
│  │   history    │  │ - missions    │  │
│  └──────────────┘  └───────────────┘  │
└────────────────────────────────────────┘
```

**핵심 원칙**:
1. ✅ **스키마 간 FK 없음**: 논리적 참조만, 나중에 DB 분리 가능
2. ✅ **서비스별 권한 분리**: auth_user, game_user
3. ✅ **Lazy Evaluation**: 일일 리셋은 접속 시 체크
4. ✅ **인메모리 우선**: 세션, 채굴 진행도는 메모리 관리
5. ✅ **로그 최소화**: 중요 트랜잭션만 DB, 나머지는 파일

---

## 2. 아키텍처 전략

### 2-1. 데이터 접근 패턴

```
┌──────────────────┐
│  Auth Service    │
│   (NodeJS)       │
└────────┬─────────┘
         │
         ↓ auth_schema만 접근
    ┌────────────┐
    │auth_schema │
    │ - users    │
    │ - jwt_*    │
    └────────────┘

┌──────────────────┐
│  Game Server     │
│    (C++)         │
└────────┬─────────┘
         │
         ↓ game_schema만 접근
    ┌────────────┐
    │game_schema │
    │ - user_*   │
    │ - pickaxe_*│
    └────────────┘
```

**Auth Service**: 
- 로그인 시 JWT 발급
- 토큰 검증 (Game Server 요청)
- auth_schema만 접근

**Game Server**:
- 연결 시 Auth Service에 JWT 검증 (1회)
- user_id 획득 후 game_schema만 접근
- auth_schema 접근 없음

---

### 2-2. 인메모리 vs DB 저장

| 데이터 | 저장 위치 | 이유 |
|--------|----------|------|
| **세션 상태** | 인메모리 | 초당 업데이트, DB 부하 큼 |
| **채굴 진행도** | 인메모리 + 스냅샷 | 초당 업데이트, 완료 시만 DB |
| **유저 재화** | DB (실시간 동기화) | 무결성 중요 |
| **곡괭이 스탯** | DB + 메모리 캐시 | 변경 빈도 낮음 |
| **일반 로그** | 파일 | 대용량, 감사용 |
| **중요 트랜잭션** | DB | IAP, 슬롯 해금 등 |

---

### 2-3. 일일 리셋 전략 (Lazy Evaluation)

**❌ 기존 방식 (일괄 업데이트)**:
```sql
-- 매일 00:00에 모든 유저 업데이트 (비효율!)
UPDATE users SET ad_count_today = 0;  -- 수만 건
```

**✅ 개선 방식 (접속 시 체크)**:
```sql
-- 접속한 유저만 체크 및 리셋
SELECT 
    ad_count_today,
    CASE 
        WHEN ad_reset_date < CURRENT_DATE THEN 0
        ELSE ad_count_today
    END AS current_ad_count
FROM user_game_data
WHERE user_id = $1;

-- 필요한 경우에만 업데이트
UPDATE user_game_data 
SET 
    ad_count_today = 0,
    ad_reset_date = CURRENT_DATE
WHERE 
    user_id = $1 
    AND ad_reset_date < CURRENT_DATE;
```

---

### 2-4. Redis 캐싱 / 인메모리 운용 (MVP)

- **왜 Redis?** 초당 업데이트/읽기가 많은 상태(채굴 진행, 세션 캐시, 레이트 리미트)는 DB I/O를 피하고자 인메모리/Redis로 관리.
- **영속 우선 순위**
  - **즉시 DB 영속**: 재화(`gold`, `crystal`), 슬롯 해금, IAP 등 민감 데이터.
  - **Redis 우선 + 주기/이벤트 flush**: 채굴 진행도(`mining_snapshots` 실시간 상태), 세션 캐시, 일일 카운터 캐시(접속 시 Lazy reset 후 DB 반영).
- **인메모리 → Redis**
  - 주기: 60초마다 dirty 여부 확인 후 변경 시에만 Redis 업서트.
  - 이벤트: 채굴 완료, 광물 교체, 서버 종료 시그널 시 즉시 업서트.
  - TTL 부여로 오래된 키 자동 청소.
- **Redis → DB**
  - 주기: 5분마다 dirty 집합(예: `dirty_snapshots` set)을 읽어 UPSERT, 성공 시 제거.
  - 이벤트: 종료 시그널, 채굴 완료, 세션 종료 시에도 즉시 UPSERT.
  - 종료 시그널 처리: 새 작업 차단 → 인메모리 최신 상태를 Redis에 밀어넣음 → dirty 집합 배치 UPSERT → 실패분 로그 후 재기동 시 복구.
- **안전 가드**: Redis 장애 시 즉시 DB 쓰기로 폴백.

## 3. ERD (Entity Relationship Diagram)

### 3-1. auth_schema

```
┌─────────────────┐
│     users       │
│ (인증 정보)     │
└────────┬────────┘
         │ 1
         │
         │ N
    ┌────┴──────────────────┬──────────────────┐
    │                       │                  │
┌───▼──────────┐   ┌────────▼────────┐ ┌──────▼────────┐
│ jwt_families │   │ session_history │ │  ban_history  │
│(JWT 패밀리)  │   │(세션 로그)      │ │(밴 이력)      │
└───┬──────────┘   └─────────────────┘ └───────────────┘
    │ 1
    │
    │ N
┌───▼──────────┐
│  jwt_tokens  │
│(발급된 JWT)  │
└──────────────┘
```

---

### 3-2. game_schema

```
┌─────────────────┐
│ user_game_data  │
│ (게임 진행)     │
└────────┬────────┘
         │ 1
         │
         ├─────────────┬──────────────┬──────────────┐
         │ N           │ N            │ N            │
┌────────▼────────┐ ┌──▼──────────┐ ┌▼─────────────┐ ┌────────────┐
│ pickaxe_slots   │ │daily_missions│ │mining_       │ │ critical_  │
│(곡괭이 슬롯)    │ │(일일 미션)   │ │snapshots     │ │transactions│
└─────────────────┘ └──────────────┘ │(채굴 진행)   │ └────────────┘
                                      └──────────────┘
                                      
┌─────────────────┐
│ user_game_data  │
└────────┬────────┘
         │ 1
         │ N
┌────────▼────────┐
│mining_          │
│completions      │
│(완료 기록)      │
└─────────────────┘
```

---

## 4. auth_schema (인증 스키마)

### 4-1. users (유저 기본 정보)

**목적**: 인증 및 밴 관리만

```sql
CREATE SCHEMA IF NOT EXISTS auth_schema;

CREATE TABLE auth_schema.users (
    -- Primary Key
    user_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    
    -- 인증 정보
    provider VARCHAR(32) NOT NULL,              -- 예: google, apple, facebook, admin(dev)
    external_id VARCHAR(255) NOT NULL,          -- 플랫폼 고유 ID (예: 구글 sub)
    email VARCHAR(255),                         -- 소셜 제공 시만 저장, nullable
    nickname VARCHAR(32),                       -- 게임 닉네임, nullable(미설정 상태)
    
    -- 타임스탬프
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    last_login TIMESTAMP,
    last_logout TIMESTAMP,
    
    -- 밴 정보
    is_banned BOOLEAN NOT NULL DEFAULT false,
    ban_reason TEXT,
    banned_at TIMESTAMP,
    banned_until TIMESTAMP  -- NULL이면 영구 밴
);

-- 인덱스
CREATE UNIQUE INDEX idx_auth_users_provider_ext ON auth_schema.users(provider, external_id);
CREATE INDEX idx_auth_users_last_login ON auth_schema.users(last_login DESC);
CREATE INDEX idx_auth_users_banned ON auth_schema.users(is_banned) 
    WHERE is_banned = true;

-- 코멘트
COMMENT ON TABLE auth_schema.users IS '유저 인증 정보 (Auth Service 전용)';
COMMENT ON COLUMN auth_schema.users.provider IS '소셜/인증 제공자 (google, apple 등)';
COMMENT ON COLUMN auth_schema.users.external_id IS '플랫폼 고유 ID (provider와 조합하여 유니크)';
COMMENT ON COLUMN auth_schema.users.nickname IS '게임 내 표시용 닉네임(미설정 시 NULL)';
```

**주의**:
- ❌ 재화 정보 없음 (gold, crystal은 game_schema)
- ❌ 게임 진행도 없음
- ✅ 인증과 밴 관리만

---

### 4-2. jwt_families (JWT 패밀리)

**목적**: JWT 슬라이딩 세션 관리

```sql
CREATE TABLE auth_schema.jwt_families (
    -- Primary Key
    family_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    
    -- 외래키 (auth_schema 내부)
    user_id UUID NOT NULL REFERENCES auth_schema.users(user_id) ON DELETE CASCADE,
    
    -- 패밀리 정보
    device_id VARCHAR(255),
    login_ip INET,
    user_agent TEXT,
    
    -- 상태
    is_active BOOLEAN NOT NULL DEFAULT true,
    is_revoked BOOLEAN NOT NULL DEFAULT false,
    revoked_reason TEXT,
    
    -- 타임스탬프
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    last_refreshed_at TIMESTAMP NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMP NOT NULL,
    revoked_at TIMESTAMP,
    
    -- 보안
    refresh_count INTEGER NOT NULL DEFAULT 0,
    max_refresh_count INTEGER NOT NULL DEFAULT 100,
    
    CONSTRAINT chk_refresh_limit CHECK (refresh_count <= max_refresh_count)
);

-- 인덱스
CREATE INDEX idx_jwt_families_user ON auth_schema.jwt_families(user_id);
CREATE INDEX idx_jwt_families_active ON auth_schema.jwt_families(user_id, is_active) 
    WHERE is_active = true;
CREATE INDEX idx_jwt_families_expires ON auth_schema.jwt_families(expires_at);

-- 코멘트
COMMENT ON TABLE auth_schema.jwt_families IS 'JWT 슬라이딩 세션 패밀리';
COMMENT ON COLUMN auth_schema.jwt_families.refresh_count IS '현재 refresh 횟수';

-- 정책 메모
-- 기본 유효 30일, Refresh 시 슬라이딩 연장하되 최대 90일까지(또는 max_refresh_count 초과 시) 재인증 요구.
```

---

### 4-3. jwt_tokens (발급된 JWT)

**목적**: 토큰 재사용 방지, 탈취 감지

```sql
CREATE TABLE auth_schema.jwt_tokens (
    -- Primary Key
    token_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    
    -- 외래키
    family_id UUID NOT NULL REFERENCES auth_schema.jwt_families(family_id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES auth_schema.users(user_id) ON DELETE CASCADE,
    
    -- 토큰 정보
    token_hash VARCHAR(64) NOT NULL UNIQUE,  -- SHA-256 해시
    jti VARCHAR(36) NOT NULL UNIQUE,  -- JWT ID
    
    -- 상태
    is_valid BOOLEAN NOT NULL DEFAULT true,
    is_used BOOLEAN NOT NULL DEFAULT false,  -- Refresh에 사용됨
    
    -- 타임스탬프
    issued_at TIMESTAMP NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMP NOT NULL,
    used_at TIMESTAMP,
    revoked_at TIMESTAMP,
    
    -- 보안
    issued_ip INET,
    used_ip INET
);

-- 인덱스
CREATE UNIQUE INDEX idx_jwt_tokens_hash ON auth_schema.jwt_tokens(token_hash);
CREATE UNIQUE INDEX idx_jwt_tokens_jti ON auth_schema.jwt_tokens(jti);
CREATE INDEX idx_jwt_tokens_family ON auth_schema.jwt_tokens(family_id);
CREATE INDEX idx_jwt_tokens_valid ON auth_schema.jwt_tokens(family_id, is_valid) 
    WHERE is_valid = true;

-- 코멘트
COMMENT ON TABLE auth_schema.jwt_tokens IS '발급된 JWT 추적 (재사용 방지)';
COMMENT ON COLUMN auth_schema.jwt_tokens.is_used IS 'Refresh에 사용됨 → 재사용 시 탈취 의심';
```

---

### 4-4. session_history (세션/인증 로그)

**목적**: 인증/접속 로그. 실시간 세션 관리는 인메모리/Redis, 여기서는 감사/분석용으로만 저장.

```sql
CREATE TABLE auth_schema.session_history (
    -- Primary Key
    session_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    
    -- 외래키
    user_id UUID NOT NULL REFERENCES auth_schema.users(user_id) ON DELETE CASCADE,
    
    -- 인증 정보 스냅샷
    provider VARCHAR(32) NOT NULL,
    external_id VARCHAR(255) NOT NULL,
    device_id VARCHAR(255),
    client_ip INET,
    user_agent TEXT,
    client_version VARCHAR(32),
    
    -- 타임스탬프
    login_at TIMESTAMP NOT NULL DEFAULT NOW(),
    logout_at TIMESTAMP,
    duration_seconds INTEGER,
    
    -- 종료 사유
    disconnect_reason TEXT  -- 'CLIENT_DISCONNECT','TOKEN_INVALID','REPLACED','TIMEOUT','BANNED','SERVER_RESTART'
);

-- 인덱스
CREATE INDEX idx_session_history_user ON auth_schema.session_history(user_id);
CREATE INDEX idx_session_history_login ON auth_schema.session_history(login_at DESC);
CREATE INDEX idx_session_history_provider ON auth_schema.session_history(provider, external_id);

-- 코멘트
COMMENT ON TABLE auth_schema.session_history IS '인증/세션 로그 (감사/분석용)';
COMMENT ON COLUMN auth_schema.session_history.disconnect_reason IS '연결 종료/인증 실패 사유';
```

**중요**: 
- 로그인 시점에 INSERT, 로그아웃/토큰 무효 시 logout_at/duration 업데이트(가능한 경우).  
- Game Server의 실시간 세션 제어는 인메모리/Redis에서 처리하고, 최소 정보만 여기 누적.  

---

### 4-5. ban_history (밴 이력)

**목적**: 밴/언밴 이력 추적

```sql
CREATE TABLE auth_schema.ban_history (
    -- Primary Key
    ban_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    
    -- 외래키
    user_id UUID NOT NULL REFERENCES auth_schema.users(user_id) ON DELETE CASCADE,
    
    -- 밴 정보
    ban_reason TEXT NOT NULL,
    ban_type VARCHAR(20) NOT NULL,  -- 'TEMPORARY', 'PERMANENT'
    
    -- 관리자 정보
    banned_by VARCHAR(100) NOT NULL DEFAULT 'SYSTEM',
    
    -- 타임스탬프
    banned_at TIMESTAMP NOT NULL DEFAULT NOW(),
    banned_until TIMESTAMP,  -- NULL이면 영구
    unbanned_at TIMESTAMP,
    unban_reason TEXT
);

-- 인덱스
CREATE INDEX idx_ban_history_user ON auth_schema.ban_history(user_id);
CREATE INDEX idx_ban_history_time ON auth_schema.ban_history(banned_at DESC);

-- 코멘트
COMMENT ON TABLE auth_schema.ban_history IS '유저 밴/언밴 이력';
```

---

## 5. game_schema (게임 스키마)

### 5-1. user_game_data (유저 게임 데이터)

**목적**: 재화, 진행도, 게임 관련 모든 정보

```sql
CREATE SCHEMA IF NOT EXISTS game_schema;

CREATE TABLE game_schema.user_game_data (
    -- Primary Key (auth_schema.users 논리적 참조, FK 없음!)
    user_id UUID PRIMARY KEY,
    
    -- 재화
    gold BIGINT NOT NULL DEFAULT 0 CHECK (gold >= 0),
    crystal INTEGER NOT NULL DEFAULT 0 CHECK (crystal >= 0),
    
    -- 진행 상황
    total_mining_count BIGINT NOT NULL DEFAULT 0,
    highest_pickaxe_level INTEGER NOT NULL DEFAULT 0,
    
    -- 슬롯 해금 상태
    unlocked_slots BOOLEAN[4] NOT NULL DEFAULT ARRAY[true, false, false, false],
    
    -- 일일 리셋 (Lazy Evaluation)
    ad_count_today INTEGER NOT NULL DEFAULT 0,
    ad_reset_date DATE NOT NULL DEFAULT CURRENT_DATE,
    mission_reroll_free INTEGER NOT NULL DEFAULT 2,
    mission_reroll_ad INTEGER NOT NULL DEFAULT 3,
    mission_reset_date DATE NOT NULL DEFAULT CURRENT_DATE,
    
    -- 오프라인 보상
    max_offline_hours INTEGER NOT NULL DEFAULT 3,  -- 기본 3시간
    
    -- 치트 탐지
    cheat_score INTEGER NOT NULL DEFAULT 0,
    
    -- 타임스탬프
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

-- 인덱스
CREATE INDEX idx_user_game_gold ON game_schema.user_game_data(gold DESC);
CREATE INDEX idx_user_game_level ON game_schema.user_game_data(highest_pickaxe_level DESC);

-- 코멘트
COMMENT ON TABLE game_schema.user_game_data IS '유저 게임 데이터 (Game Server 전용)';
COMMENT ON COLUMN game_schema.user_game_data.user_id IS 'auth_schema.users 논리적 참조 (FK 없음)';
COMMENT ON COLUMN game_schema.user_game_data.ad_reset_date IS 'Lazy Evaluation: 접속 시 CURRENT_DATE 비교';
```

**중요**:
- ❌ `user_id`에 FK 없음 (스키마 독립성)
- ✅ Lazy Evaluation: 리셋 필드는 접속 시 체크

---

### 5-2. pickaxe_slots (곡괭이 슬롯)

```sql
CREATE TABLE game_schema.pickaxe_slots (
    -- Primary Key
    slot_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    
    -- 외래키 (game_schema 내부, FK 없음)
    user_id UUID NOT NULL,
    
    -- 슬롯 정보
    slot_index INTEGER NOT NULL CHECK (slot_index BETWEEN 0 AND 3),
    
    -- 곡괭이 스탯
    level INTEGER NOT NULL DEFAULT 0 CHECK (level >= 0 AND level <= 100),
    tier INTEGER NOT NULL DEFAULT 1 CHECK (tier BETWEEN 1 AND 5),
    dps BIGINT NOT NULL DEFAULT 10 CHECK (dps > 0),
    pity_bonus INTEGER NOT NULL DEFAULT 0 CHECK (pity_bonus >= 0 AND pity_bonus <= 10000), -- basis 10000 = 100.00%
    
    -- 타임스탬프
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    last_upgraded_at TIMESTAMP,
    
    -- 제약조건
    CONSTRAINT uq_user_slot UNIQUE (user_id, slot_index)
);

-- 인덱스
CREATE INDEX idx_pickaxe_user ON game_schema.pickaxe_slots(user_id);
CREATE INDEX idx_pickaxe_level ON game_schema.pickaxe_slots(level DESC);

-- 코멘트
COMMENT ON TABLE game_schema.pickaxe_slots IS '유저 곡괭이 슬롯';
COMMENT ON COLUMN game_schema.pickaxe_slots.slot_index IS '슬롯 번호 (0-3)';
COMMENT ON COLUMN game_schema.pickaxe_slots.pity_bonus IS '강화 실패 누적 보너스 (basis 10000 = 100.00%)';
```

---

### 5-3. mining_snapshots (채굴 스냅샷)

**목적**: 서버 재시작 대비 주기적 스냅샷 (5분마다)

```sql
CREATE TABLE game_schema.mining_snapshots (
    -- Primary Key
    snapshot_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    
    -- 외래키
    user_id UUID NOT NULL,
    
    -- 광물 정보
    mineral_id INTEGER NOT NULL CHECK (mineral_id BETWEEN 0 AND 6),
    
    -- 진행 상황
    current_hp BIGINT NOT NULL CHECK (current_hp >= 0),
    max_hp BIGINT NOT NULL,
    mining_start_time TIMESTAMP NOT NULL,
    
    -- 스냅샷 시간
    snapshot_time TIMESTAMP NOT NULL DEFAULT NOW(),
    
    -- 제약조건: 유저당 광물 하나씩만
    CONSTRAINT uq_user_mineral UNIQUE (user_id, mineral_id)
);

-- 인덱스
CREATE INDEX idx_mining_snapshots_user ON game_schema.mining_snapshots(user_id);
CREATE INDEX idx_mining_snapshots_time ON game_schema.mining_snapshots(snapshot_time);

-- 코멘트
COMMENT ON TABLE game_schema.mining_snapshots IS '채굴 진행 스냅샷 (서버 재시작 복구용)';
COMMENT ON COLUMN game_schema.mining_snapshots.snapshot_time IS '마지막 스냅샷 시간';
```

**사용 방식**:
- 실시간 채굴은 인메모리/Redis
- Redis → 3~5분 주기 또는 채굴 완료/서버 종료 시 DB UPSERT(내구성)
- 서버 재시작 시 DB 최신 스냅샷 로드 후 Redis 복원

---

### 5-4. mining_completions (채굴 완료 기록)

**목적**: 완료된 채굴 통계

```sql
CREATE TABLE game_schema.mining_completions (
    -- Primary Key
    completion_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    
    -- 외래키
    user_id UUID NOT NULL,
    
    -- 광물 정보
    mineral_id INTEGER NOT NULL,
    
    -- 보상
    gold_earned BIGINT NOT NULL CHECK (gold_earned >= 0),
    
    -- 통계
    mining_duration_seconds INTEGER NOT NULL,
    
    -- 타임스탬프
    completed_at TIMESTAMP NOT NULL DEFAULT NOW()
);

-- 인덱스
CREATE INDEX idx_mining_completions_user ON game_schema.mining_completions(user_id);
CREATE INDEX idx_mining_completions_time ON game_schema.mining_completions(completed_at DESC);
CREATE INDEX idx_mining_completions_mineral ON game_schema.mining_completions(mineral_id);

-- 코멘트
COMMENT ON TABLE game_schema.mining_completions IS '채굴 완료 기록 (통계용)';
```

---

### 5-5. daily_missions (일일 미션)

```sql
CREATE TABLE game_schema.daily_missions (
    -- Primary Key
    mission_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    
    -- 외래키
    user_id UUID NOT NULL,
    
    -- 미션 정보
    mission_index INTEGER NOT NULL CHECK (mission_index BETWEEN 0 AND 6),
    mission_type VARCHAR(50) NOT NULL,  -- MINE_COUNT, GOLD_EARN, UPGRADE_ONCE 등
    
    -- 미션 목표
    target_value INTEGER NOT NULL,
    current_value INTEGER NOT NULL DEFAULT 0,
    reward_crystal INTEGER NOT NULL,
    
    -- 상태
    is_completed BOOLEAN NOT NULL DEFAULT false,
    is_claimed BOOLEAN NOT NULL DEFAULT false,
    
    -- 타임스탬프
    assigned_at TIMESTAMP NOT NULL DEFAULT NOW(),
    completed_at TIMESTAMP,
    claimed_at TIMESTAMP,
    reset_at TIMESTAMP NOT NULL,  -- 다음 KST 00:00
    
    -- 제약조건
    CONSTRAINT uq_user_mission_index UNIQUE (user_id, mission_index, reset_at)
);

-- 인덱스
CREATE INDEX idx_missions_user ON game_schema.daily_missions(user_id);
CREATE INDEX idx_missions_active ON game_schema.daily_missions(user_id, is_completed, is_claimed);
CREATE INDEX idx_missions_reset ON game_schema.daily_missions(reset_at);

-- 코멘트
COMMENT ON TABLE game_schema.daily_missions IS '유저 일일 미션';
COMMENT ON COLUMN game_schema.daily_missions.reset_at IS '미션 리셋 시간';
```

---

### 5-6. critical_transactions (중요 트랜잭션)

**목적**: IAP, 슬롯 해금 등 중요한 거래만 기록

```sql
CREATE TABLE game_schema.critical_transactions (
    -- Primary Key
    transaction_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    
    -- 외래키
    user_id UUID NOT NULL,
    
    -- 트랜잭션 정보
    transaction_type VARCHAR(50) NOT NULL,  -- IAP, SLOT_UNLOCK, REFUND
    
    -- 재화 변동
    gold_delta BIGINT NOT NULL DEFAULT 0,
    crystal_delta INTEGER NOT NULL DEFAULT 0,
    
    -- 잔액 (트랜잭션 후)
    gold_after BIGINT NOT NULL,
    crystal_after INTEGER NOT NULL,
    
    -- 메타데이터
    metadata JSONB,
    
    -- 타임스탬프
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    
    -- 제약조건
    CONSTRAINT chk_critical_types CHECK (
        transaction_type IN ('IAP', 'SLOT_UNLOCK', 'REFUND', 'ADMIN_ADJUST')
    )
);

-- 인덱스
CREATE INDEX idx_critical_tx_user ON game_schema.critical_transactions(user_id);
CREATE INDEX idx_critical_tx_time ON game_schema.critical_transactions(created_at DESC);
CREATE INDEX idx_critical_tx_type ON game_schema.critical_transactions(transaction_type);
CREATE INDEX idx_critical_tx_metadata ON game_schema.critical_transactions USING GIN (metadata);

-- 코멘트
COMMENT ON TABLE game_schema.critical_transactions IS '중요 트랜잭션만 기록 (IAP, 슬롯 등)';
COMMENT ON COLUMN game_schema.critical_transactions.metadata IS '추가 정보 JSON';
```

**일반 트랜잭션 (채굴, 강화)**:
- 파일 로그로 기록 (spdlog 등)
- DB 저장 안 함 (성능)

---

## 6. 인덱스 전략

### 6-1. 복합 인덱스 설계

**원칙**:
1. Selectivity 높은 컬럼을 앞에
2. 등호 조건 → 범위 조건 순서
3. 정렬 컬럼 포함

**예시**:
```sql
-- 좋은 예: 유저의 활성 패밀리
CREATE INDEX idx_jwt_families_active 
    ON auth_schema.jwt_families(user_id, is_active) 
    WHERE is_active = true;

-- 좋은 예: 유저의 최근 미션
CREATE INDEX idx_missions_active 
    ON game_schema.daily_missions(user_id, reset_at DESC)
    WHERE is_claimed = false;
```

---

### 6-2. Partial Index (조건부 인덱스)

```sql
-- 밴된 유저만 (Cardinality 낮지만 조건부로 유용)
CREATE INDEX idx_users_banned 
    ON auth_schema.users(is_banned) 
    WHERE is_banned = true;

-- 유효한 토큰만
CREATE INDEX idx_jwt_tokens_valid 
    ON auth_schema.jwt_tokens(family_id, is_valid) 
    WHERE is_valid = true;
```

---

### 6-3. GIN 인덱스 (JSONB)

```sql
-- JSONB 메타데이터 검색
CREATE INDEX idx_critical_tx_metadata 
    ON game_schema.critical_transactions 
    USING GIN (metadata);

-- 사용 예시
SELECT * FROM critical_transactions 
WHERE metadata @> '{"slot_index": 2}';
```

---

## 7. 트랜잭션 정책

### 7-1. 격리 수준

**기본**: `READ COMMITTED`
- 대부분의 게임 로직에 적합
- Dirty Read 방지

**특수 상황**: `SERIALIZABLE`
```sql
-- 중요 트랜잭션 (슬롯 해금, IAP)
BEGIN TRANSACTION ISOLATION LEVEL SERIALIZABLE;
-- ...
COMMIT;
```

---

### 7-2. 트랜잭션 패턴

#### **Pattern 1: 강화 (Upgrade, 확률 + 천장)**

```sql
BEGIN;

-- 1. 메타 조회 (애플리케이션 레벨에서 기본 확률/보정률 계산)
-- base_rate = meta(tier/level), bonus_rate = 읽기, final_rate = min(base+bonus, 100%)
-- 클라이언트에는 final_rate, base_rate, bonus_rate 반환

-- 2. 확률 판정 (서버)
-- 성공/실패 결정 (성공 시 bonus=0, 실패 시 bonus += base*0.1, 상한 100)

-- 3. 골드 차감 (성공/실패 공통)
UPDATE game_schema.user_game_data 
SET 
    gold = gold - $cost,
    updated_at = NOW()
WHERE 
    user_id = $1 
    AND gold >= $cost
RETURNING gold;

-- 반환값 없으면 ROLLBACK (INSUFFICIENT_GOLD)

-- 4. 결과 적용
-- 성공 시: level/dps 업데이트, bonus=0
-- 실패 시: bonus 갱신, level/dps 유지
UPDATE game_schema.pickaxe_slots 
SET 
    level = CASE WHEN $success THEN level + 1 ELSE level END,
    dps = CASE WHEN $success THEN $new_dps ELSE dps END,
    pity_bonus = CASE WHEN $success THEN 0 ELSE LEAST(pity_bonus + $bonus_add, 10000) END, -- basis 10000 = 100.00%
    last_upgraded_at = NOW(),
    updated_at = NOW()
WHERE 
    user_id = $1 
    AND slot_index = $slot_index;

-- 5. 통계 업데이트 (성공 시)
UPDATE game_schema.user_game_data
SET 
    highest_pickaxe_level = GREATEST(highest_pickaxe_level, $new_level)
WHERE user_id = $1;

-- 6. 로그 (선택) upgrade_history 등에 기록
COMMIT;
```

---

#### **Pattern 2: JWT Refresh (슬라이딩 세션)**

```sql
BEGIN;

-- 1. 기존 토큰 무효화
UPDATE auth_schema.jwt_tokens 
SET 
    is_valid = false,
    is_used = true,
    used_at = NOW(),
    used_ip = $2
WHERE 
    jti = $1 
    AND is_valid = true
    AND is_used = false
RETURNING family_id, user_id;

-- 반환값 없으면 탈취 의심!
-- → jwt_families 무효화 (별도 트랜잭션)

-- 2. Family 업데이트
UPDATE auth_schema.jwt_families 
SET 
    last_refreshed_at = NOW(),
    refresh_count = refresh_count + 1
WHERE 
    family_id = $3
    AND is_active = true
    AND refresh_count < max_refresh_count
RETURNING refresh_count;

-- 3. 새 토큰 발급
INSERT INTO auth_schema.jwt_tokens (
    family_id, user_id, token_hash, jti, 
    expires_at, issued_ip
) VALUES (
    $3, $4, $5, $6, 
    NOW() + INTERVAL '7 days', $7
);

COMMIT;
```

---

#### **Pattern 3: 채굴 완료**

```sql
BEGIN;

-- 1. 골드 지급
UPDATE game_schema.user_game_data 
SET 
    gold = gold + 140,
    total_mining_count = total_mining_count + 1,
    updated_at = NOW()
WHERE user_id = $1
RETURNING gold;

-- 2. 완료 기록
INSERT INTO game_schema.mining_completions (
    user_id, mineral_id, gold_earned, mining_duration_seconds
) VALUES (
    $1, 3, 140, 5
);

-- 3. 스냅샷 삭제 (완료됨)
DELETE FROM game_schema.mining_snapshots
WHERE user_id = $1 AND mineral_id = 3;

COMMIT;
```

---

## 8. 쿼리 예시

### 8-1. 로그인 (Auth Service)

```sql
-- 1. 유저 조회 또는 생성
INSERT INTO auth_schema.users (google_id, username, device_id, last_login)
VALUES ($1, $2, $3, NOW())
ON CONFLICT (google_id) 
DO UPDATE SET 
    last_login = NOW(),
    device_id = EXCLUDED.device_id
RETURNING user_id, is_banned, ban_reason;

-- 2. 밴 체크
-- (위 쿼리에서 is_banned 확인)

-- 3. JWT Family 생성
INSERT INTO auth_schema.jwt_families (
    user_id, device_id, login_ip, user_agent, expires_at
) VALUES (
    $1, $2, $3, $4, NOW() + INTERVAL '30 days'
) RETURNING family_id;

-- 4. JWT Token 발급
INSERT INTO auth_schema.jwt_tokens (
    family_id, user_id, token_hash, jti, expires_at, issued_ip
) VALUES (
    $1, $2, $3, $4, NOW() + INTERVAL '7 days', $5
) RETURNING token_id;
```

---

### 8-2. 게임 데이터 로드 (Game Server)

```sql
-- 1. 유저 게임 데이터
SELECT 
    gold,
    crystal,
    unlocked_slots,
    total_mining_count,
    highest_pickaxe_level,
    
    -- Lazy Evaluation
    CASE 
        WHEN ad_reset_date < CURRENT_DATE THEN 0
        ELSE ad_count_today
    END AS ad_count_today,
    
    CASE 
        WHEN mission_reset_date < CURRENT_DATE THEN 2
        ELSE mission_reroll_free
    END AS mission_reroll_free,
    
    max_offline_hours,
    cheat_score
FROM game_schema.user_game_data
WHERE user_id = $1;

-- 신규 유저면 초기 데이터 생성
INSERT INTO game_schema.user_game_data (user_id)
VALUES ($1)
ON CONFLICT (user_id) DO NOTHING;

-- 2. 곡괭이 슬롯
SELECT 
    slot_index,
    level,
    tier,
    dps
FROM game_schema.pickaxe_slots
WHERE user_id = $1
ORDER BY slot_index;

-- 첫 슬롯 없으면 생성
INSERT INTO game_schema.pickaxe_slots 
    (user_id, slot_index, level, tier, dps)
VALUES ($1, 0, 0, 1, 10)
ON CONFLICT (user_id, slot_index) DO NOTHING;

-- 3. 일일 미션
SELECT 
    mission_index,
    mission_type,
    target_value,
    current_value,
    reward_crystal,
    is_completed,
    is_claimed,
    reset_at
FROM game_schema.daily_missions
WHERE 
    user_id = $1 
    AND reset_at >= CURRENT_DATE;  -- 오늘 이후 미션만
```

---

### 8-3. 오프라인 보상 계산

```sql
-- 마지막 로그아웃 시간
SELECT 
    last_logout,
    max_offline_hours
FROM auth_schema.users u
JOIN game_schema.user_game_data g ON u.user_id = g.user_id
WHERE u.user_id = $1;

-- 오프라인 초 계산 (애플리케이션에서)
-- offline_seconds = min(now - last_logout, max_offline_hours * 3600)

-- 보상 지급
UPDATE game_schema.user_game_data
SET 
    gold = gold + $2,  -- 계산된 오프라인 보상
    updated_at = NOW()
WHERE user_id = $1
RETURNING gold;

-- 로그인 시간 업데이트
UPDATE auth_schema.users
SET last_login = NOW()
WHERE user_id = $1;
```

---

### 8-4. 슬롯 해금

```sql
BEGIN;

-- 1. 크리스탈 차감
UPDATE game_schema.user_game_data
SET 
    crystal = crystal - 400,
    unlocked_slots[2] = true,  -- 슬롯 2 해금
    updated_at = NOW()
WHERE 
    user_id = $1
    AND crystal >= 400
    AND unlocked_slots[2] = false
RETURNING crystal, unlocked_slots;

-- 2. 중요 트랜잭션 기록
INSERT INTO game_schema.critical_transactions (
    user_id, transaction_type, crystal_delta, crystal_after, metadata
) VALUES (
    $1, 'SLOT_UNLOCK', -400, <new_crystal>,
    '{"slot_index": 2}'::jsonb
);

COMMIT;
```

---

## 9. 권한 관리

### 9-1. DB 유저 생성

```sql
-- Auth Service 전용 유저
CREATE USER auth_user WITH PASSWORD 'strong_auth_password';
GRANT CONNECT ON DATABASE infinite_pickaxe TO auth_user;
GRANT USAGE ON SCHEMA auth_schema TO auth_user;
GRANT ALL ON ALL TABLES IN SCHEMA auth_schema TO auth_user;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA auth_schema TO auth_user;

-- Game Server 전용 유저
CREATE USER game_user WITH PASSWORD 'strong_game_password';
GRANT CONNECT ON DATABASE infinite_pickaxe TO game_user;
GRANT USAGE ON SCHEMA game_schema TO game_user;
GRANT ALL ON ALL TABLES IN SCHEMA game_schema TO game_user;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA game_schema TO game_user;

-- 크로스 스키마 읽기 (필요 시)
GRANT SELECT ON auth_schema.users TO game_user;  -- 밴 체크용 (선택)
```

---

### 9-2. 연결 설정

#### **Auth Service (NodeJS)**

```javascript
const { Pool } = require('pg');

const authPool = new Pool({
    host: process.env.DB_HOST || 'localhost',
    port: process.env.DB_PORT || 10002,
    database: 'infinite_pickaxe',
    user: 'auth_user',
    password: process.env.AUTH_DB_PASSWORD,
    // 기본 스키마 설정
    options: '-c search_path=auth_schema,public'
});

// 쿼리 시 auth_schema가 기본
const result = await authPool.query(
    'SELECT user_id FROM users WHERE google_id = $1',
    [googleId]
);
```

---

#### **Game Server (C++)**

```cpp
#include <pqxx/pqxx>

class DatabaseConnection {
private:
    pqxx::connection conn_;
    
public:
    DatabaseConnection() {
        std::string connstr = 
            "host=" + GetEnv("DB_HOST") +
            " port=" + GetEnv("DB_PORT") +
            " dbname=infinite_pickaxe" +
            " user=game_user" +
            " password=" + GetEnv("GAME_DB_PASSWORD") +
            " options='-c search_path=game_schema,public'";
        
        conn_ = pqxx::connection(connstr);
    }
    
    // 쿼리 시 game_schema가 기본
    UserGameData LoadUserData(const std::string& user_id) {
        pqxx::work txn(conn_);
        auto result = txn.exec_params(
            "SELECT gold, crystal FROM user_game_data WHERE user_id = $1",
            user_id
        );
        // ...
    }
};
```

---

## 10. 백업 및 복구

### 10-1. 백업 전략

#### **전체 백업**

```bash
# 매일 자정 (cron)
0 0 * * * pg_dump -U postgres -h localhost -p 10002 \
    infinite_pickaxe > /backup/full_$(date +\%Y\%m\%d).sql
```

#### **스키마별 백업**

```bash
# auth_schema만
pg_dump -U postgres -h localhost -p 10002 \
    -n auth_schema infinite_pickaxe > auth_backup.sql

# game_schema만
pg_dump -U postgres -h localhost -p 10002 \
    -n game_schema infinite_pickaxe > game_backup.sql
```

#### **Docker Volume 백업**

```bash
docker run --rm \
    -v postgres_data:/data \
    -v /backup:/backup \
    alpine tar czf /backup/postgres_$(date +%Y%m%d).tar.gz -C /data .
```

---

### 10-2. 복구

```bash
# 전체 복구
psql -U postgres -h localhost -p 10002 \
    infinite_pickaxe < /backup/full_20241208.sql

# 스키마별 복구
psql -U postgres -h localhost -p 10002 \
    infinite_pickaxe < auth_backup.sql
```

---

### 10-3. 보관 정책

| 백업 유형 | 빈도 | 보관 기간 |
|----------|------|----------|
| 전체 백업 | 매일 | 30일 |
| 증분 백업 (WAL) | 연속 | 7일 |
| Volume 스냅샷 | 주간 | 90일 |

---

## 11. 성능 최적화

### 11-1. 연결 풀

**권장 설정**:
```ini
# Auth Service (NodeJS)
pool_min = 2
pool_max = 10

# Game Server (C++)
pool_min = 5
pool_max = 20
```

---

### 11-2. VACUUM 설정

```sql
-- postgresql.conf
autovacuum = on
autovacuum_vacuum_scale_factor = 0.1
autovacuum_analyze_scale_factor = 0.05

-- 수동 실행
VACUUM ANALYZE game_schema.user_game_data;
```

---

### 11-3. 파티셔닝 (Phase 2)

```sql
-- mining_completions 월별 파티셔닝
CREATE TABLE game_schema.mining_completions (
    ...
) PARTITION BY RANGE (completed_at);

CREATE TABLE mining_completions_2024_12 
    PARTITION OF game_schema.mining_completions
    FOR VALUES FROM ('2024-12-01') TO ('2025-01-01');
```

---

## 12. 마이그레이션 전략

### 12-1. 스키마 버전 관리

```sql
CREATE TABLE public.migration_history (
    version INTEGER PRIMARY KEY,
    description TEXT NOT NULL,
    applied_at TIMESTAMP NOT NULL DEFAULT NOW(),
    applied_by VARCHAR(100) NOT NULL
);

INSERT INTO public.migration_history (version, description, applied_by)
VALUES (1, 'Initial schema with auth and game separation', 'system');
```

---

### 12-2. 초기화 스크립트 구조

```
database/
├── init.sql                           # 메인 스크립트
│
├── auth_schema/
│   ├── 01_create_schema.sql
│   ├── 02_create_tables.sql
│   ├── 03_create_indexes.sql
│   ├── 04_create_triggers.sql
│   └── 05_grant_permissions.sql
│
├── game_schema/
│   ├── 01_create_schema.sql
│   ├── 02_create_tables.sql
│   ├── 03_create_indexes.sql
│   ├── 04_create_triggers.sql
│   └── 05_grant_permissions.sql
│
└── common/
    ├── 01_extensions.sql              # uuid-ossp 등
    └── 02_functions.sql               # 공용 함수
```

---

### 12-3. init.sql

```sql
-- PostgreSQL 15 초기화 스크립트
-- /docker-entrypoint-initdb.d/init.sql

-- 1. 확장 설치
\i /docker-entrypoint-initdb.d/common/01_extensions.sql

-- 2. 공용 함수
\i /docker-entrypoint-initdb.d/common/02_functions.sql

-- 3. auth_schema
\i /docker-entrypoint-initdb.d/auth_schema/01_create_schema.sql
\i /docker-entrypoint-initdb.d/auth_schema/02_create_tables.sql
\i /docker-entrypoint-initdb.d/auth_schema/03_create_indexes.sql
\i /docker-entrypoint-initdb.d/auth_schema/04_create_triggers.sql
\i /docker-entrypoint-initdb.d/auth_schema/05_grant_permissions.sql

-- 4. game_schema
\i /docker-entrypoint-initdb.d/game_schema/01_create_schema.sql
\i /docker-entrypoint-initdb.d/game_schema/02_create_tables.sql
\i /docker-entrypoint-initdb.d/game_schema/03_create_indexes.sql
\i /docker-entrypoint-initdb.d/game_schema/04_create_triggers.sql
\i /docker-entrypoint-initdb.d/game_schema/05_grant_permissions.sql

-- 완료
SELECT 'Database initialization completed!' AS status;
```

---

### 12-4. updated_at 트리거

```sql
-- common/02_functions.sql

-- 트리거 함수 생성
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;
```

```sql
-- auth_schema/04_create_triggers.sql

-- auth_schema.users
CREATE TRIGGER update_users_updated_at
    BEFORE UPDATE ON auth_schema.users
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- jwt_families (updated_at 없음 - 트리거 제외)
-- jwt_tokens (updated_at 없음 - 트리거 제외)
```

```sql
-- game_schema/04_create_triggers.sql

-- user_game_data
CREATE TRIGGER update_user_game_data_updated_at
    BEFORE UPDATE ON game_schema.user_game_data
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- pickaxe_slots
CREATE TRIGGER update_pickaxe_slots_updated_at
    BEFORE UPDATE ON game_schema.pickaxe_slots
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- mining_snapshots, mining_completions (updated_at 없음)
-- daily_missions (updated_at 없음)
```

---

### 12-5. 최근 마이그레이션 메모 (MVP)
- auth_schema.users: provider/external_id/email/nickname 구조로 변경, google_id/username/device_id 제거, 유니크 인덱스(provider, external_id).  
- auth_schema.session_history: 인증 로그 중심(provider, external_id, device_id, client_ip, login_at/logout_at, disconnect_reason).  
- RefreshToken 슬라이딩 정책: 기본 30일, Refresh 시 최대 90일까지 연장 후 재인증 필수.  
- 마이그레이션 파일 예시: `20251212_provider_external_id.sql`, `20251212_session_history_redefine.sql`.

---

## 13. DB 분리 시나리오 (Phase 2+)

### 13-1. 현재 (스키마 분리)

```
PostgreSQL (물리 1개)
├── auth_schema
└── game_schema
```

### 13-2. 미래 (물리 분리)

```sql
-- 1. 새 DB 생성
CREATE DATABASE auth_db;
CREATE DATABASE game_db;

-- 2. 스키마 덤프 및 복원
pg_dump -n auth_schema infinite_pickaxe > auth_schema.sql
psql auth_db < auth_schema.sql

pg_dump -n game_schema infinite_pickaxe > game_schema.sql
psql game_db < game_schema.sql

-- 3. 애플리케이션 설정만 변경
-- Before: database=infinite_pickaxe
-- After: database=auth_db (Auth Service)
--        database=game_db (Game Server)
```

**장점**:
- ✅ FK 없어서 분리 쉬움
- ✅ 애플리케이션 코드 변경 없음
- ✅ 스키마 이름 유지

---

## 14. 문서 변경 이력

| 버전 | 날짜 | 변경 내용 |
|------|------|----------|
| 1.0 | 2025-12-08 | 초안 작성 (단일 스키마) |
| 2.0 | 2025-12-08 | 스키마 분리, 인메모리 세션, Lazy Evaluation 적용 |
| 3.0 | 2025-12-09 | Redis 운용/플러시 세부 전략 업데이트 |
| 4.0 | 2025-12-12 | 인증 스키마 구조 변경 및 마이그레이션 |

---

## 15. 요약

### ✅ 핵심 설계 원칙

1. **스키마 분리**: auth_schema, game_schema
2. **FK 없음**: 스키마 간 논리적 참조만
3. **Lazy Evaluation**: 일일 리셋은 접속 시 체크
4. **인메모리 우선**: 세션, 채굴 진행도
5. **로그 최소화**: 중요 트랜잭션만 DB
6. **권한 분리**: auth_user, game_user
7. **나중에 분리 용이**: 물리 DB 분리 시 쉬움

---

**문서 끝**
