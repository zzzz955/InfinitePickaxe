# 무한의 곡괭이: 네트워크 프로토콜 명세서
## Technical Design Document - Network Protocol

**버전**: 1.0 (MVP)  
**작성일**: 2024-12-08  
**문서 유형**: 기술 설계 문서 - 네트워크 프로토콜  

---

## 목차
1. [전체 통신 구조](#1-전체-통신-구조)
2. [인증 서버 프로토콜](#2-인증-서버-프로토콜)
3. [게임 서버 프로토콜](#3-게임-서버-프로토콜)
4. [에러 코드 체계](#4-에러-코드-체계)
5. [패킷 검증 방식](#5-패킷-검증-방식)
6. [채굴 동기화 설계](#6-채굴-동기화-설계)
7. [연결 관리](#7-연결-관리)
8. [보안 고려사항](#8-보안-고려사항)

---

## 1. 전체 통신 구조

### 1-1. 아키텍처 개요

```
┌──────────────────────┐
│   Unity 클라이언트    │
│   (Android)          │
└─────┬──────────┬─────┘
      │          │
      │ REST     │ TCP
      │ HTTPS    │ JSON
      │ :10000   │ :10001
      ↓          ↓
┌──────────┐  ┌──────────┐
│ 인증 서버 │  │ 게임 서버 │
│ NodeJS   │←─│ C++      │
│ :10000   │  │ :10001   │
└────┬─────┘  └────┬─────┘
     │             │
     └──────┬──────┘
            ↓
    ┌──────────────┐
    │ PostgreSQL   │
    │ :10002       │
    └──────────────┘
```

### 1-2. 프로토콜 역할 분리

| 통신 경로 | 프로토콜 | 포트 | 용도 |
|----------|---------|------|------|
| 클라이언트 ↔ 인증 서버 | HTTPS + JSON | 10000 | 로그인, JWT 발급 |
| 클라이언트 ↔ 게임 서버 | TCP + JSON | 10001 | 실시간 게임 로직 |
| 게임 서버 ↔ 인증 서버 | HTTP (내부) | 3000 | JWT 검증 |
| 서버 ↔ DB | PostgreSQL | 10002 | 데이터 영속화 |

---

## 2. 인증 서버 프로토콜

### 2-1. 기술 스택
- **프레임워크**: NodeJS 20 LTS + Express 4.x
- **프로토콜**: HTTPS (Let's Encrypt)
- **데이터 포맷**: JSON
- **인증 방식**: Google Play Games + JWT

### 2-2. API 엔드포인트

#### **POST /auth/login**
로그인 및 회원가입 처리

**요청**:
```json
{
  "google_token": "eyJhbGciOiJSUzI1NiIsImtpZCI6...",
  "device_id": "uuid-1234-5678-abcd",
  "client_version": "1.0.0"
}
```

**응답 (성공 200)**:
```json
{
  "success": true,
  "jwt": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "user_id": "uuid-user-1234",
  "is_new_user": false,
  "server_time": 1701234567890
}
```

**응답 (실패 401)**:
```json
{
  "success": false,
  "error_code": "INVALID_TOKEN",
  "error_message": "Invalid Google token"
}
```

---

#### **POST /auth/verify**
JWT 검증 (게임 서버 전용)

**요청**:
```json
{
  "jwt": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

**응답 (성공 200)**:
```json
{
  "valid": true,
  "user_id": "uuid-user-1234",
  "google_id": "12345678",
  "expires_at": 1701841367
}
```

**응답 (실패 401)**:
```json
{
  "valid": false,
  "error_code": "TOKEN_EXPIRED",
  "error_message": "JWT token has expired"
}
```

---

#### **POST /auth/logout**
로그아웃 처리

**요청 헤더**:
```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**응답 (200)**:
```json
{
  "success": true
}
```

---

#### **GET /auth/profile**
프로필 조회

**요청 헤더**:
```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**응답 (200)**:
```json
{
  "user_id": "uuid-user-1234",
  "google_id": "12345678",
  "username": "Player123",
  "created_at": "2024-11-28T12:34:56Z",
  "last_login": "2024-12-08T10:20:30Z"
}
```

---

### 2-3. JWT 구조

**JWT Payload**:
```json
{
  "user_id": "uuid-user-1234",
  "google_id": "12345678",
  "iat": 1701234567,
  "exp": 1701841367
}
```

**유효 기간**: 7일  
**서명 알고리즘**: HS256  
**Secret**: 환경 변수 `JWT_SECRET`으로 관리  

---

## 3. 게임 서버 프로토콜

### 3-0. MVP 메시지 요약 (proto 전환용)

| 메시지명 | 방향 | 목적/설명 | 핵심 필드(요약) |
| --- | --- | --- | --- |
| HandshakeReq | C→S | JWT 전달 및 버전/디바이스 식별 | `jwt`, `client_version`, `device_id` |
| HandshakeRes | S→C | 인증 결과 + 초기 스냅샷 | `ok`, `error`, `user_id`, `device_id`, `google_id`, `user_data`(gold/crystal/slots/current_mineral 등) |
| Heartbeat (Ping) | C→S | 연결 유지/지연 측정 | `client_time_ms` |
| HeartbeatAck (Pong) | S→C | Heartbeat 응답 | `server_time_ms` |
| MiningStart | C→S | 광물 선택 및 채굴 시작 | `mineral_id` |
| MiningSync | C→S | 채굴 진행 검증용 보고(1초) | `mineral_id`, `client_hp`, `client_timestamp` |
| MiningUpdate | S→C | 채굴 진행 브로드캐스트(1초) | `mineral_id`, `current_hp`, `max_hp`, `damage_dealt`, `server_timestamp` |
| MiningComplete | S→C | 채굴 완료/보상 | `mineral_id`, `gold_earned`, `total_gold`, `mining_count`, `respawn_time`, `server_timestamp` |
| UpgradePickaxe | C→S | 곡괭이 강화 요청 | `slot_index`, `target_level` |
| UpgradeResult | S→C | 강화 결과 | `success`, `slot_index`, `new_level`, `new_dps`, `gold_spent`, `remaining_gold`, `error_code` |
| MissionClaim | C→S | 일일 미션 보상 수령 | `mission_index` |
| MissionReroll | C→S | 일일 미션 리롤 | `use_ad` |
| MissionUpdate | S→C | 미션 상태/리셋 정보 | `missions[]`(index/type/target/current/reward/completed/claimed), `milestones`, `reset_time` |
| SlotUnlock | C→S | 슬롯 해금 요청 | `slot_index` |
| SlotUnlockResult | S→C | 슬롯 해금 결과 | `success`, `slot_index`, `crystal_spent`, `remaining_crystal`, `error_code` |
| OfflineRewardRequest | C→S | 오프라인 보상 계산 요청 | `request_type` |
| OfflineReward | S→C | 오프라인 보상 정산 | `offline_seconds`, `gold_earned`, `mining_cycles`, `mineral_id`, `efficiency`, `new_total_gold` |
| Error | S→C | 공통 에러 응답 | `error_code`, `error_message`, `detail`(선택) |

### 3-1. TCP 패킷 구조

```
┌─────────────────────────────────────────────────┐
│              패킷 헤더 (16 bytes)                │
├────────┬────────┬────────┬────────┬────────────┤
│ Magic  │ Length │ Type   │ Seq    │ Timestamp  │
│ 2bytes │ 4bytes │ 2bytes │ 4bytes │ 4bytes     │
└────────┴────────┴────────┴────────┴────────────┘
│                                                  │
│           페이로드 (JSON, N bytes)               │
│                                                  │
└─────────────────────────────────────────────────┘
```

#### **헤더 필드 상세**

| 필드 | 크기 | 타입 | 설명 | 비고 |
|------|------|------|------|------|
| **Magic** | 2 bytes | uint16 | 고정값 `0x5049` | "PI" = Pickaxe Infinite |
| **Length** | 4 bytes | uint32 | 페이로드 길이 | 최대 64KB |
| **Type** | 2 bytes | uint16 | 메시지 타입 | 0x0000 - 0x1FFF |
| **Seq** | 4 bytes | uint32 | 시퀀스 번호 | 패킷 순서 보장 |
| **Timestamp** | 4 bytes | uint32 | Unix timestamp | 초 단위 |

**Endianness**: Little-Endian (네트워크 바이트 순서)

---

### 3-2. 메시지 타입 정의 (동기화 주기)

- 진행 스냅샷(채굴): 서버→클라 `MiningUpdate` 0.25~0.5초(2~4Hz) 간격, 혹은 HP 누적 변화량 5~10% 시 즉시 전송
- 완료/보상: 채굴 완료 시 즉시 전송
- 하트비트: 30초 간격

#### **클라이언트 → 서버 (0x0000 - 0x0FFF)**

| 메시지 타입 | Type ID | 메시지명 | 설명 |
|------------|---------|----------|------|
| **인증** | 0x0001 | ClientAuth | JWT 토큰 전송 |
| **하트비트** | 0x0002 | Heartbeat | 연결 유지 (30초마다) |
| | | | |
| **채굴 시작** | 0x0100 | MiningStart | 광물 선택 & 채굴 시작 |
| **채굴 동기화** | 0x0101 | MiningSync | 채굴 상태 동기화 (1초마다) |
| | | | |
| **곡괭이 강화** | 0x0200 | UpgradePickaxe | 곡괭이 레벨업 |
| | | | |
| **미션 보상** | 0x0300 | MissionClaim | 일일 미션 보상 수령 |
| **미션 재설정** | 0x0301 | MissionReroll | 일일 미션 리롤 |
| | | | |
| **슬롯 해금** | 0x0400 | SlotUnlock | 곡괭이 슬롯 해금 |
| | | | |
| **오프라인 보상** | 0x0500 | OfflineRewardRequest | 오프라인 보상 요청 |

#### **서버 → 클라이언트 (0x1000 - 0x1FFF)**

| 메시지 타입 | Type ID | 메시지명 | 설명 |
|------------|---------|----------|------|
| **인증 결과** | 0x1001 | AuthResult | 인증 성공/실패 |
| **하트비트 응답** | 0x1002 | HeartbeatAck | 하트비트 확인 |
| | | | |
| **채굴 진행** | 0x1100 | MiningUpdate | 채굴 진행 상황 (1초마다) |
| **채굴 완료** | 0x1101 | MiningComplete | 채굴 완료 & 보상 |
| | | | |
| **강화 결과** | 0x1200 | UpgradeResult | 강화 성공/실패 |
| | | | |
| **미션 업데이트** | 0x1300 | MissionUpdate | 미션 상태 변경 |
| | | | |
| **슬롯 해금 결과** | 0x1400 | SlotUnlockResult | 슬롯 해금 성공/실패 |
| | | | |
| **오프라인 보상** | 0x1500 | OfflineReward | 오프라인 보상 정산 |
| | | | |
| **에러** | 0x1FFF | Error | 에러 메시지 |

---

### 3-3. JSON 페이로드 스키마

#### **ClientAuth (0x0001)**
클라이언트가 서버에 인증 요청

```json
{
  "jwt": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "client_version": "1.0.0",
  "device_id": "uuid-device-1234"
}
```

---

#### **AuthResult (0x1001)**
서버가 인증 결과 응답

**성공**:
```json
{
  "success": true,
  "user_data": {
    "user_id": "uuid-user-1234",
    "pickaxe_level": 12,
    "pickaxe_dps": 1638,
    "gold": 5230,
    "crystal": 150,
    "unlocked_slots": [true, true, false, false],
    "current_mineral_id": 3,
    "mineral_hp": 800,
    "last_logout": 1701234567
  },
  "server_time": 1701234890
}
```

**실패**:
```json
{
  "success": false,
  "error_code": "AUTH_FAILED",
  "error_message": "Invalid JWT token"
}
```

---

#### **Heartbeat (0x0002)**
클라이언트가 30초마다 전송

```json
{
  "ping": true
}
```

#### **HeartbeatAck (0x1002)**
서버가 즉시 응답

```json
{
  "pong": true,
  "server_time": 1701234890
}
```

---

#### **MiningStart (0x0100)**
광물 선택 & 채굴 시작

```json
{
  "mineral_id": 3
}
```

---

#### **MiningSync (0x0101)**
클라이언트가 1초마다 전송 (검증용)

```json
{
  "mineral_id": 3,
  "client_hp": 800,
  "client_timestamp": 1701234567
}
```

---

#### **MiningUpdate (0x1100)**
서버가 1초마다 응답

```json
{
  "mineral_id": 3,
  "current_hp": 800,
  "max_hp": 1500,
  "damage_dealt": 1638,
  "server_timestamp": 1701234567
}
```

---

#### **MiningComplete (0x1101)**
채굴 완료 시 서버가 전송

```json
{
  "mineral_id": 3,
  "gold_earned": 140,
  "total_gold": 5370,
  "mining_count": 1,
  "respawn_time": 5,
  "server_timestamp": 1701234570
}
```

---

#### **UpgradePickaxe (0x0200)**
곡괭이 강화 요청

```json
{
  "slot_index": 0,
  "target_level": 13
}
```

---

#### **UpgradeResult (0x1200)**
강화 결과

**성공**:
```json
{
  "success": true,
  "slot_index": 0,
  "new_level": 13,
  "new_dps": 2310,
  "gold_spent": 3500,
  "remaining_gold": 1870,
  "server_timestamp": 1701234580
}
```

**실패**:
```json
{
  "success": false,
  "error_code": "INSUFFICIENT_GOLD",
  "error_message": "Not enough gold to upgrade",
  "required": 3500,
  "current": 1870
}
```

---

#### **MissionClaim (0x0300)**
미션 보상 수령

```json
{
  "mission_index": 2
}
```

---

#### **MissionUpdate (0x1300)**
미션 상태 업데이트

```json
{
  "missions": [
    {
      "index": 0,
      "type": "MINE_COUNT",
      "description": "광물 10회 채굴",
      "target": 10,
      "current": 5,
      "reward_crystal": 10,
      "completed": false,
      "claimed": false
    },
    {
      "index": 1,
      "type": "UPGRADE_ONCE",
      "description": "곡괭이 1회 강화",
      "target": 1,
      "current": 1,
      "reward_crystal": 10,
      "completed": true,
      "claimed": false
    },
    {
      "index": 2,
      "type": "GOLD_EARN",
      "description": "골드 5,000 획득",
      "target": 5000,
      "current": 3200,
      "reward_crystal": 14,
      "completed": false,
      "claimed": false
    }
  ],
  "milestones": {
    "completed_3": false,
    "completed_5": false,
    "completed_7": false,
    "offline_bonus_hours": 0
  },
  "reset_time": 1701273600
}
```

---

#### **MissionReroll (0x0301)**
미션 재설정 요청

```json
{
  "use_ad": false
}
```

**응답**: MissionUpdate (0x1300) 패킷

---

#### **SlotUnlock (0x0400)**
슬롯 해금 요청

```json
{
  "slot_index": 2
}
```

---

#### **SlotUnlockResult (0x1400)**
슬롯 해금 결과

```json
{
  "success": true,
  "slot_index": 2,
  "crystal_spent": 2000,
  "remaining_crystal": 150
}
```

---

#### **OfflineRewardRequest (0x0500)**
오프라인 보상 요청

```json
{
  "request_type": "calculate"
}
```

---

#### **OfflineReward (0x1500)**
오프라인 보상 정산

```json
{
  "offline_seconds": 7200,
  "gold_earned": 18450,
  "mining_cycles": 142,
  "mineral_id": 3,
  "mineral_name": "구리",
  "efficiency": 0.5,
  "new_total_gold": 23680
}
```

---

#### **Error (0x1FFF)**
에러 메시지

```json
{
  "error_code": "INSUFFICIENT_GOLD",
  "error_message": "Not enough gold to upgrade",
  "detail": {
    "required": 3500,
    "current": 1870
  }
}
```

---

## 4. 에러 코드 체계

### 4-1. 에러 코드 범위

| 범위 | 카테고리 | 설명 |
|------|---------|------|
| 1000-1099 | 인증 | 인증/토큰 관련 |
| 2000-2099 | 프로토콜 | 패킷 형식/검증 |
| 3000-3099 | 게임 로직 | 게임 규칙 위반 |
| 5000-5099 | 서버 | 서버 내부 오류 |

### 4-2. 에러 코드 목록

| 코드 | 이름 | 설명 | HTTP 상태 |
|------|------|------|----------|
| **1001** | AUTH_FAILED | 인증 실패 | 401 |
| **1002** | INVALID_TOKEN | 잘못된 토큰 | 401 |
| **1003** | TOKEN_EXPIRED | 토큰 만료 | 401 |
| **1004** | SESSION_NOT_FOUND | 세션 없음 | 401 |
| | | | |
| **2001** | INVALID_PACKET | 패킷 형식 오류 | 400 |
| **2002** | INVALID_SEQUENCE | 시퀀스 번호 오류 | 400 |
| **2003** | TIMESTAMP_MISMATCH | 타임스탬프 불일치 | 400 |
| **2004** | INVALID_JSON | JSON 파싱 실패 | 400 |
| **2005** | RATE_LIMIT_EXCEEDED | 요청 빈도 초과 | 429 |
| | | | |
| **3001** | INSUFFICIENT_GOLD | 골드 부족 | 400 |
| **3002** | INSUFFICIENT_CRYSTAL | 크리스탈 부족 | 400 |
| **3003** | INVALID_LEVEL | 잘못된 레벨 | 400 |
| **3004** | MINERAL_NOT_AVAILABLE | 광물 재등장 대기 중 | 400 |
| **3005** | ALREADY_MINING | 이미 채굴 중 | 400 |
| **3006** | NOT_MINING | 채굴 중이 아님 | 400 |
| **3007** | MISSION_NOT_COMPLETED | 미션 미완료 | 400 |
| **3008** | MISSION_ALREADY_CLAIMED | 이미 보상 수령 | 400 |
| **3009** | SLOT_ALREADY_UNLOCKED | 슬롯 이미 해금됨 | 400 |
| **3010** | INVALID_MINERAL_ID | 존재하지 않는 광물 | 400 |
| **3011** | INVALID_SLOT_INDEX | 잘못된 슬롯 인덱스 | 400 |
| | | | |
| **5000** | SERVER_ERROR | 서버 내부 오류 | 500 |
| **5001** | DB_ERROR | 데이터베이스 오류 | 500 |
| **5002** | AUTH_SERVICE_ERROR | 인증 서비스 오류 | 503 |

---

## 5. 패킷 검증 방식

### 5-1. 시퀀스 번호 검증

**목적**: 패킷 순서 보장, 리플레이 공격 방지

**알고리즘**:
```cpp
// 서버 측 의사코드
class Session {
    uint32_t expected_seq_ = 1;
    
    bool ValidateSequence(uint32_t recv_seq) {
        // 1. 정확히 일치하는지 확인
        if (recv_seq == expected_seq_) {
            expected_seq_++;
            return true;
        }
        
        // 2. 순서가 틀린 경우
        if (recv_seq < expected_seq_) {
            // 과거 패킷 (리플레이 공격 의심)
            LogWarning("Old packet received: expected={}, received={}",
                      expected_seq_, recv_seq);
            return false;
        }
        
        // 3. 시퀀스가 너무 앞선 경우
        if (recv_seq > expected_seq_ + 10) {
            // 패킷 대량 유실 or 조작
            LogError("Sequence jump too large: expected={}, received={}",
                    expected_seq_, recv_seq);
            return false;
        }
        
        // 4. 약간의 유실은 허용 (UDP처럼)
        LogWarning("Packet loss detected: skipped {} packets",
                   recv_seq - expected_seq_);
        expected_seq_ = recv_seq + 1;
        return true;
    }
};
```

> **운영 정책 (TBD)**: MVP 단계에서는 자동 밴을 실행하지 않고 탐지 로그와 GM 알림 훅(TBD)에만 남긴 뒤 수동 판정 후 제재한다. 스코어는 DB에 누적 저장해 사후 조사를 지원한다.


---

### 5-2. 타임스탬프 검증

**목적**: 시간 조작 치트 방지

**알고리즘**:
```cpp
bool ValidateTimestamp(uint32_t client_ts) {
    uint32_t server_ts = GetCurrentUnixTimestamp();
    int32_t diff = std::abs((int32_t)(server_ts - client_ts));
    
    // ±60초 이내만 허용
    const int32_t MAX_TIME_DIFF = 60;
    
    if (diff > MAX_TIME_DIFF) {
        LogWarning("Timestamp too far: client={}, server={}, diff={}s",
                   client_ts, server_ts, diff);
        
        // 치트 스코어 증가
        cheat_score_++;
        
        if (cheat_score_ >= 5) {
            BanUser("Time manipulation detected");
            return false;
        }
    }
    
    return true;
}
```

**허용 범위**: ±60초
- 클라이언트/서버 시간 차이 고려
- 네트워크 지연 고려

---

### 5-3. 패킷 무결성 검증

**알고리즘**:
```cpp
bool ValidatePacket(const uint8_t* data, size_t size) {
    // 1. 최소 크기 확인
    if (size < sizeof(PacketHeader)) {
        return false;
    }
    
    // 2. 헤더 파싱
    const PacketHeader* header = 
        reinterpret_cast<const PacketHeader*>(data);
    
    // 3. Magic 번호 확인
    if (header->magic != 0x5049) {
        LogWarning("Invalid magic: 0x{:04X}", header->magic);
        return false;
    }
    
    // 4. 길이 확인
    size_t expected_size = sizeof(PacketHeader) + header->length;
    if (size != expected_size) {
        LogWarning("Size mismatch: expected={}, actual={}",
                   expected_size, size);
        return false;
    }
    
    // 5. 메시지 타입 범위 확인
    if (header->type > 0x1FFF) {
        LogWarning("Invalid message type: 0x{:04X}", header->type);
        return false;
    }
    
    // 6. JSON 파싱 가능한지 확인
    const uint8_t* payload = data + sizeof(PacketHeader);
    try {
        nlohmann::json j = nlohmann::json::parse(
            payload, 
            payload + header->length
        );
        return true;
    } catch (const std::exception& e) {
        LogWarning("JSON parse error: {}", e.what());
        return false;
    }
}
```

---

### 5-4. DPS 검증 (치트 탐지)

**목적**: DPS 조작 탐지

**알고리즘**:
```cpp
bool ValidateMiningProgress(const MiningSync& sync) {
    // 1. 서버에서 예상 HP 계산
    auto elapsed = GetServerTime() - mining_start_time_;
    auto expected_hp = initial_hp_ - (user_dps_ * elapsed);
    
    // 2. 클라이언트 HP와 비교
    auto diff = std::abs(expected_hp - sync.client_hp);
    auto tolerance = expected_hp * 0.1f;  // 10% 오차 허용
    
    // 3. 오차 범위 체크
    if (diff > tolerance) {
        LogSuspicious("DPS mismatch: expected={}, client={}, diff={}",
                     expected_hp, sync.client_hp, diff);
        
        cheat_score_++;
        
        // 4. 누적 치트 스코어 확인
        if (cheat_score_ >= 5) {
            BanUser("DPS manipulation detected");
            return false;
        }
    }
    
    return true;
}
```

---

## 6. 채굴 동기화 설계

### 6-1. 1초 틱 기반 시뮬레이션

**서버 권위 모델**:
- 모든 채굴 로직은 **서버에서 시뮬레이션**
- 클라이언트는 **렌더링만 수행**
- 1초마다 **동기화 패킷 전송**

**타임라인**:
```
[클라이언트]                    [서버]
     │                            │
     │ MiningStart (mineral=3)    │
     ├───────────────────────────>│
     │                            │ DB: mineral 3 상태 확인
     │                            │ 채굴 시작 시간 저장
     │                            │ initial_hp = 1500
     │                            │
     │ MiningUpdate (초기 상태)   │
     │<───────────────────────────┤
     │ (hp=1500, max=1500)        │
     │                            │
     │ [HP 바 표시 시작]          │
     │                            │
[1초 경과]                        │
     │                            │ [서버 틱]
     │                            │ elapsed = 1초
     │                            │ hp = 1500 - 1638 = -138
     │                            │ → 채굴 완료!
     │                            │
     │ MiningComplete             │
     │<───────────────────────────┤
     │ (gold=140)                 │
     │                            │ DB: gold 업데이트
     │                            │ DB: mineral respawn 타이머
     │                            │
     │ [완료 연출]                │
     │ [골드 획득 팝업]           │
     │                            │
```

---

### 6-2. 클라이언트 보간 처리

**Unity 코드 예시**:
```csharp
public class MiningController : MonoBehaviour 
{
    // 서버 동기화 데이터
    private float serverHP;
    private float maxHP;
    
    // 클라이언트 렌더링용
    private float displayHP;
    private float dps;
    
    void Start() {
        // 초기화
        displayHP = maxHP;
    }
    
    void Update() {
        // 서버 HP로 부드럽게 보간
        displayHP = Mathf.Lerp(
            displayHP, 
            serverHP, 
            Time.deltaTime * 5f
        );
        
        // UI 업데이트
        UpdateHPBar(displayHP, maxHP);
        UpdateHPText(displayHP, maxHP);
    }
    
    // 서버에서 MiningUpdate 수신 시
    void OnMiningUpdate(MiningUpdatePacket packet) {
        serverHP = packet.current_hp;
        maxHP = packet.max_hp;
        
        // 데미지 텍스트 표시
        ShowDamageText(packet.damage_dealt);
    }
    
    // 서버에서 MiningComplete 수신 시
    void OnMiningComplete(MiningCompletePacket packet) {
        // 완료 연출
        PlayCompleteAnimation();
        ShowGoldEarnedPopup(packet.gold_earned);
        
        // 재등장 타이머 시작
        StartRespawnTimer(packet.mineral_id, packet.respawn_time);
    }
}
```

---

### 6-3. 재등장 시스템

**서버 로직**:
```cpp
void OnMiningComplete(Session* session, int mineral_id) {
    // 1. 보상 지급
    auto gold = GetMineralGold(mineral_id);
    session->user_data.gold += gold;
    
    // 2. DB 업데이트
    UpdateUserGold(session->user_id, session->user_data.gold);
    IncrementMiningCount(session->user_id, mineral_id);
    
    // 3. 재등장 타이머 시작 (5초)
    session->mineral_respawn_time[mineral_id] = GetServerTime() + 5;
    
    // 4. 클라이언트에 완료 패킷 전송
    SendMiningComplete(session, mineral_id, gold);
}

bool IsMineralAvailable(Session* session, int mineral_id) {
    auto now = GetServerTime();
    auto respawn_time = session->mineral_respawn_time[mineral_id];
    
    return now >= respawn_time;
}
```

**클라이언트 UI**:
```
채굴 완료 시:
┌─────────────────┐
│   💎 구리       │
│                 │
│   ⏱️ 5초       │  ← 카운트다운
│                 │
│   [선택 불가]   │  ← 버튼 비활성화
└─────────────────┘

5초 후:
┌─────────────────┐
│   💎 구리       │
│                 │
│   ✨ 준비됨!    │
│                 │
│   [선택]        │  ← 버튼 활성화
└─────────────────┘
```

---

## 7. 연결 관리

### 7-1. 하트비트 메커니즘

**목적**: 
- 연결 유지
- 좀비 세션 정리
- 네트워크 지연 측정

**프로토콜**:
```
클라이언트: 30초마다 Heartbeat (0x0002) 전송
서버: 즉시 HeartbeatAck (0x1002) 응답

타임아웃: 60초 동안 패킷 없으면 연결 끊기
```

**타임라인**:
```
[클라이언트]              [서버]
     │                      │
     │ Heartbeat            │
     ├─────────────────────>│
     │                      │ last_active = now
     │ HeartbeatAck         │
     │<─────────────────────┤
     │ (RTT 측정 가능)      │
     │                      │
[30초 대기]
     │                      │
     │ Heartbeat            │
     ├─────────────────────>│
     │                      │ last_active = now
     │                      │
[연결 끊김]
     X                      │
                            │ [타이머 체크]
                            │ if (now - last_active > 60s)
                            │   CloseSession()
```

---

### 7-2. 재접속 처리

**시나리오 1: 정상 재접속**
```cpp
void OnClientReconnect(TcpSocket* socket, const string& jwt) {
    // 1. JWT 검증 (인증 서버 호출)
    auto user_id = VerifyJWT(jwt);
    if (user_id.empty()) {
        SendAuthFailed(socket);
        socket->Close();
        return;
    }
    
    // 2. 기존 세션 확인
    auto old_session = FindSession(user_id);
    if (old_session) {
        // 기존 연결 강제 종료
        old_session->Close("Reconnected from another device");
        RemoveSession(user_id);
    }
    
    // 3. 오프라인 보상 계산
    auto user_data = LoadUserData(user_id);
    auto reward = CalculateOfflineReward(user_data);
    
    // 4. 유저 데이터 업데이트
    user_data.gold += reward.gold;
    user_data.last_login = GetServerTime();
    SaveUserData(user_data);
    
    // 5. 새 세션 생성
    auto new_session = CreateSession(socket, user_data);
    
    // 6. AuthResult + OfflineReward 전송
    SendAuthResult(new_session, user_data);
    if (reward.offline_seconds > 0) {
        SendOfflineReward(new_session, reward);
    }
}
```

---

### 7-3. 비정상 종료 처리

**클라이언트 크래시**:
```cpp
// 서버는 하트비트 타임아웃으로 감지
void OnHeartbeatTimeout(Session* session) {
    LogInfo("Client timeout: user={}", session->user_id);
    
    // 1. 현재 상태 저장
    SaveUserProgress(session);
    
    // 2. 오프라인 모드 진입
    session->is_offline = true;
    session->offline_start_time = GetServerTime();
    
    // 3. 세션 정리 (메모리 해제)
    RemoveSession(session->user_id);
}
```

**서버 재시작**:
```cpp
// 서버 시작 시 모든 유저 오프라인 상태로 전환
void OnServerStart() {
    auto active_sessions = db_->LoadActiveSessions();
    
    for (auto& session : active_sessions) {
        // 오프라인 타임스탬프 기록
        session.offline_start_time = GetServerTime();
        session.is_offline = true;
        
        db_->UpdateSession(session);
    }
    
    LogInfo("Marked {} sessions as offline", active_sessions.size());
}
```

---

## 8. 보안 고려사항

### 8-1. Rate Limiting

**목적**: DDoS 방어, 스팸 방지

**구현**:
```cpp
class RateLimiter {
private:
    struct Limit {
        uint32_t max_per_second;
        uint32_t max_per_minute;
    };
    
    std::map<uint16_t, Limit> limits_ = {
        {0x0001, {1, 5}},      // Auth: 초당 1회, 분당 5회
        {0x0100, {10, 600}},   // MiningStart: 초당 10회
        {0x0200, {1, 60}},     // Upgrade: 초당 1회
        {0x0300, {1, 10}},     // MissionClaim: 분당 10회
        {0x0400, {1, 5}},      // SlotUnlock: 분당 5회
    };
    
    std::map<uint16_t, uint32_t> second_count_;
    std::map<uint16_t, uint32_t> minute_count_;
    
public:
    bool CheckLimit(uint16_t msg_type) {
        auto& limit = limits_[msg_type];
        
        // 초당 제한 확인
        if (++second_count_[msg_type] > limit.max_per_second) {
            LogWarning("Rate limit exceeded: type=0x{:04X}, rate=second",
                      msg_type);
            return false;
        }
        
        // 분당 제한 확인
        if (++minute_count_[msg_type] > limit.max_per_minute) {
            LogWarning("Rate limit exceeded: type=0x{:04X}, rate=minute",
                      msg_type);
            return false;
        }
        
        return true;
    }
    
    void ResetPerSecond() {
        second_count_.clear();
    }
    
    void ResetPerMinute() {
        minute_count_.clear();
    }
};
```

> **운영 정책 (TBD)**: MVP 단계에서는 자동 밴을 실행하지 않고 탐지 로그와 GM 알림 훅(TBD)에만 남긴 뒤 수동 판정 후 제재한다. 스코어는 DB에 누적 저장해 사후 조사에 활용한다.

---

### 8-2. 치트 탐지 시스템

**치트 스코어 메커니즘**:
```cpp
class CheatDetector {
private:
    struct CheatScore {
        int dps_manipulation = 0;
        int timestamp_anomaly = 0;
        int packet_anomaly = 0;
        int total = 0;
    };
    
    std::map<string, CheatScore> scores_;
    
    const int BAN_THRESHOLD = 10;
    
public:
    void ReportDPSCheat(const string& user_id) {
        auto& score = scores_[user_id];
        score.dps_manipulation++;
        score.total++;
        
        LogSuspicious("DPS cheat suspected: user={}, score={}",
                     user_id, score.total);
        
        CheckBan(user_id, score);
    }
    
    void ReportTimestampAnomaly(const string& user_id) {
        auto& score = scores_[user_id];
        score.timestamp_anomaly++;
        score.total++;
        
        LogSuspicious("Timestamp anomaly: user={}, score={}",
                     user_id, score.total);
        
        CheckBan(user_id, score);
    }
    
    void ReportPacketAnomaly(const string& user_id) {
        auto& score = scores_[user_id];
        score.packet_anomaly++;
        score.total++;
        
        LogSuspicious("Packet anomaly: user={}, score={}",
                     user_id, score.total);
        
        CheckBan(user_id, score);
    }
    
private:
    void CheckBan(const string& user_id, const CheatScore& score) {
        if (score.total >= BAN_THRESHOLD) {
            LogError("User banned for cheating: user={}, score={}",
                    user_id, score.total);
            
            BanUser(user_id, "Automated cheat detection");
            
            // 세션 강제 종료
            if (auto session = FindSession(user_id)) {
                session->Close("Account banned");
            }
        }
    }
};
```

---

### 8-3. SQL Injection 방어

**Prepared Statement 사용**:
```cpp
// 안전한 예시
void UpdateUserGold(const string& user_id, int64_t gold) {
    pqxx::work txn(db_connection_);
    
    txn.exec_params(
        "UPDATE users SET gold = $1, updated_at = NOW() WHERE user_id = $2",
        gold,
        user_id
    );
    
    txn.commit();
}

// 위험한 예시 (절대 사용 금지)
void UpdateUserGoldUnsafe(const string& user_id, int64_t gold) {
    string query = "UPDATE users SET gold = " + 
                   std::to_string(gold) + 
                   " WHERE user_id = '" + user_id + "'";
    // SQL Injection 취약!
}
```

---

### 8-4. JWT Secret 관리

**환경 변수 사용**:
```bash
# .env 파일 (Git에 커밋하지 않음)
JWT_SECRET=your-super-secret-key-min-32-chars-long
DB_PASSWORD=your-database-password
GOOGLE_CLIENT_ID=your-google-client-id
```

**Docker Compose**:
```yaml
services:
  auth-server:
    environment:
      - JWT_SECRET=${JWT_SECRET}
    env_file:
      - .env
```

**코드**:
```javascript
// NodeJS
const jwt = require('jsonwebtoken');
const secret = process.env.JWT_SECRET;

if (!secret || secret.length < 32) {
    throw new Error('JWT_SECRET must be at least 32 characters');
}

// JWT 발급
const token = jwt.sign(
    { user_id, google_id },
    secret,
    { expiresIn: '7d' }
);
```

---

## 9. 향후 확장 고려사항

### 9-1. Protobuf 전환 (Phase 2)

**현재 (JSON)**:
```json
{"mineral_id": 3, "client_hp": 800}
→ 38 bytes
```

**Protobuf 전환 시**:
```protobuf
message MiningSync {
    int32 mineral_id = 1;
    float client_hp = 2;
}
→ 8 bytes (약 5배 절약)
```

---

### 9-2. TCP TLS 암호화 (Phase 2)

**현재**: 평문 TCP  
**Phase 2**: TLS 1.3 추가

**장점**:
- 패킷 스니핑 방어
- 중간자 공격 방어

**단점**:
- CPU 오버헤드 (+10%)
- 구현 복잡도 증가

> **개발/로컬 가이드**: 로컬·개발 환경은 자체 서명 인증서나 dev CA, Docker/Nginx TLS termination으로 검증하고, 프로덕션은 Certbot 인증서를 공유하되 가상 호스트/별도 FQDN으로 다른 게임과 충돌을 피한다.

---

### 9-3. WebSocket 지원 (Phase 3)

**목적**: 웹 클라이언트 지원

**변경점**:
- 헤더 구조는 동일 유지
- 전송 계층만 TCP → WebSocket 변경

---

## 10. 문서 변경 이력

| 버전 | 날짜 | 변경 내용 |
|------|------|----------|
| 1.0 | 2024-12-08 | 초안 작성 (MVP 프로토콜 명세) |

---

**문서 끝**
