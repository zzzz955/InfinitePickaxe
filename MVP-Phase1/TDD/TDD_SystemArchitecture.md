# 무한의 곡괭이: 시스템 아키텍처 설계
## Technical Design Document - System Architecture

**버전**: 1.0 (MVP)  
**작성일**: 2024-12-08  
**문서 유형**: 기술 설계 문서 - 시스템 아키텍처  

---

## 목차
1. [아키텍처 개요](#1-아키텍처-개요)
2. [시스템 구성도](#2-시스템-구성도)
3. [컴포넌트 상세](#3-컴포넌트-상세)
4. [데이터 흐름](#4-데이터-흐름)
5. [배포 전략](#5-배포-전략)
6. [네트워크 구성](#6-네트워크-구성)
7. [확장성 전략](#7-확장성-전략)
8. [모니터링 및 로깅](#8-모니터링-및-로깅)
9. [보안 아키텍처](#9-보안-아키텍처)
10. [장애 대응](#10-장애-대응)
11. [성능 목표](#11-성능-목표)
12. [Phase별 진화 계획](#12-phase별-진화-계획)

---

## 1. 아키텍처 개요

### 1-1. 핵심 설계 철학

**"Modular Monolith" - 간단하게 시작, 유연하게 확장**

```
┌─────────────────────────────────────────────┐
│         설계 원칙                           │
├─────────────────────────────────────────────┤
│ 1. MVP 복잡도 최소화                        │
│ 2. 1-3명 팀 운영 가능                       │
│ 3. 낮은 인프라 비용                         │
│ 4. 나중에 MSA 전환 가능                     │
│ 5. 성능 우선 (실시간 게임)                  │
└─────────────────────────────────────────────┘
```

---

### 1-2. 아키텍처 스타일

**Modular Monolith + 선택적 분리**

| 컴포넌트 | 배포 단위 | 이유 |
|----------|----------|------|
| **Auth Service** | 독립 프로세스 (NodeJS) | 빠른 개발, REST API 적합 |
| **Game Server** | 독립 프로세스 (C++) | 고성능, Modular 내부 구조 |
| **PostgreSQL** | 단일 DB, 스키마 분리 | 트랜잭션 단순화, 나중 분리 쉬움 |
| **File Logs** | 로컬 파일 | 성능, 간단함 |

**vs MSA 비교**:
- ✅ 레이턴시 낮음 (네트워크 홉 최소)
- ✅ 배포 간단 (2개 프로세스)
- ✅ 디버깅 쉬움
- ⚠️ 확장성 제한적 (수직 확장 위주)

---

### 1-3. 기술 스택

| 레이어 | 기술 | 버전 | 선택 이유 |
|--------|------|------|----------|
| **클라이언트** | Unity | 2022.3 LTS | 안정성, 광고 SDK 호환 |
| **인증 서버** | NodeJS + Express | 20.x LTS | 빠른 개발, JWT 라이브러리 풍부 |
| **게임 서버** | C++ + Boost.asio | C++17 | 고성능 TCP, 메모리 효율 |
| **데이터베이스** | PostgreSQL | 15.x | JSONB, 고급 기능, 안정성 |
| **컨테이너** | Docker + Compose | 24.x | 간편한 배포, 일관된 환경 |
| **로깅** | spdlog (C++) | 1.12.x | 고성능 파일 로깅 |
| **HTTP 클라이언트** | cpp-httplib (C++) | 0.14.x | 헤더 온리, 간단 |

---

## 2. 시스템 구성도

### 2-1. 전체 아키텍처

```
┌───────────────────────────────────────────────────────────┐
│                     인터넷                                │
└────────────────────────┬──────────────────────────────────┘
                         │
                         ↓
              ┌──────────────────────┐
              │   홈서버 공유기      │
              │   (포트포워딩)       │
              └──────────┬───────────┘
                         │
        ┌────────────────┼────────────────┐
        │                │                │
        ↓ :10000         ↓ :10001         ↓ :10002
┌───────────────┐  ┌──────────────┐  ┌──────────────┐
│ Auth Service  │  │ Game Server  │  │ PostgreSQL   │
│   (NodeJS)    │  │    (C++)     │  │    15.x      │
│               │  │              │  │              │
│ HTTPS/REST    │  │  TCP/JSON    │  │ auth_schema  │
│               │◄─┤              │  │ game_schema  │
│ JWT 발급/검증 │  │  Modular:    │  └──────┬───────┘
│               │  │  ┌─────────┐ │         │
└───────────────┘  │  │ Game    │ │         │
                   │  │ Module  │ │         │
                   │  ├─────────┤ │         │
                   │  │ User    │ ├─────────┘
                   │  │ Module  │ │
                   │  ├─────────┤ │
                   │  │ Mission │ │
                   │  │ Module  │ │
                   │  └─────────┘ │
                   └──────┬───────┘
                          │
                          ↓
                   ┌──────────────┐
                   │  File Logs   │
                   │  /logs/*.log │
                   └──────────────┘

┌─────────────────────────────────────────────┐
│          Unity 클라이언트 (Android)         │
│                                             │
│  ┌────────────┐  ┌────────────┐            │
│  │ HTTPS      │  │ TCP        │            │
│  │ REST API   │  │ Persistent │            │
│  │ (로그인)   │  │ (게임)     │            │
│  └────────────┘  └────────────┘            │
└─────────────────────────────────────────────┘
```

---

### 2-2. 컴포넌트 다이어그램

```
┌──────────────────────────────────────────────────────────────┐
│                        Docker Compose                        │
│                                                              │
│  ┌─────────────────┐  ┌──────────────────┐  ┌────────────┐ │
│  │ auth-server     │  │ game-server      │  │ postgres   │ │
│  │ Port: 10000     │  │ Port: 10001      │  │ Port: 10002│ │
│  │ Network: bridge │  │ Network: bridge  │  │ Network:   │ │
│  │                 │  │                  │  │   bridge   │ │
│  │ Volumes:        │  │ Volumes:         │  │            │ │
│  │ - ./auth-server │  │ - ./game-server  │  │ Volumes:   │ │
│  │ - /logs/auth    │  │ - /logs/game     │  │ - postgres │ │
│  │                 │  │                  │  │   _data    │ │
│  └─────────────────┘  └──────────────────┘  └────────────┘ │
│                                                              │
│  Environment:                                                │
│  - .env 파일에서 로드                                        │
│  - JWT_SECRET, DB_PASSWORD, GOOGLE_CLIENT_ID                │
└──────────────────────────────────────────────────────────────┘
```

---

## 3. 컴포넌트 상세

### 3-1. Unity 클라이언트

#### **책임**
- 렌더링 및 UI
- 유저 입력 처리
- Auth Service에 로그인 (HTTPS)
- Game Server와 TCP 연결 유지
- 서버 결과 표시 (보간 처리)

#### **기술 스택**
```yaml
엔진: Unity 2022.3 LTS
언어: C#
플랫폼: Android (APK)
광고: Unity Ads / AdMob
인증: Google Play Games Services
```

#### **주요 클래스**

```csharp
// NetworkManager.cs
public class NetworkManager : MonoBehaviour {
    // HTTPS - Auth Service
    private HttpClient authClient;
    
    // TCP - Game Server
    private TcpClient gameClient;
    private NetworkStream stream;
    
    public async Task<string> Login(string googleToken) {
        var response = await authClient.PostAsync(
            "https://your-domain.com:10000/auth/login",
            new { google_token = googleToken }
        );
        var jwt = await response.Content.ReadAsStringAsync();
        return jwt;
    }
    
    public void ConnectToGameServer(string jwt) {
        gameClient = new TcpClient("your-domain.com", 10001);
        stream = gameClient.GetStream();
        
        // ClientAuth 패킷 전송
        SendPacket(new ClientAuthPacket { jwt = jwt });
    }
    
    private void Update() {
        // 수신 패킷 처리
        if (stream.DataAvailable) {
            var packet = ReceivePacket();
            ProcessPacket(packet);
        }
    }
}
```

---

### 3-2. Auth Service (NodeJS)

#### **책임**
- Google Play Games 토큰 검증
- JWT 발급/갱신/무효화
- 토큰 검증 API (Game Server용)
- 밴 관리
- auth_schema만 접근

#### **디렉토리 구조**

```
auth-server/
├── src/
│   ├── routes/
│   │   ├── auth.js          # 로그인, 검증, refresh
│   │   └── admin.js         # 밴 관리 (Phase 2)
│   ├── middleware/
│   │   ├── jwt.js           # JWT 검증 미들웨어
│   │   └── rateLimit.js     # Rate Limiting
│   ├── services/
│   │   ├── googleAuth.js    # Google 토큰 검증
│   │   ├── jwtService.js    # JWT 발급/검증
│   │   └── userService.js   # 유저 CRUD
│   ├── db/
│   │   └── postgres.js      # DB 연결 (auth_schema)
│   └── app.js               # Express 앱
├── package.json
├── .env.example
└── Dockerfile
```

#### **주요 코드**

```javascript
// src/routes/auth.js
const express = require('express');
const router = express.Router();
const googleAuth = require('../services/googleAuth');
const jwtService = require('../services/jwtService');
const db = require('../db/postgres');

// 로그인
router.post('/login', async (req, res) => {
    try {
        const { google_token, device_id } = req.body;
        
        // 1. Google 토큰 검증
        const googleId = await googleAuth.verify(google_token);
        
        // 2. 유저 조회 또는 생성
        const result = await db.query(`
            INSERT INTO auth_schema.users (google_id, device_id, last_login)
            VALUES ($1, $2, NOW())
            ON CONFLICT (google_id) 
            DO UPDATE SET 
                last_login = NOW(),
                device_id = EXCLUDED.device_id
            RETURNING user_id, is_banned, ban_reason
        `, [googleId, device_id]);
        
        const user = result.rows[0];
        
        // 3. 밴 체크
        if (user.is_banned) {
            return res.status(403).json({ 
                error: 'BANNED',
                reason: user.ban_reason 
            });
        }
        
        // 4. JWT 발급
        const jwt = await jwtService.issue(user.user_id, device_id);
        
        res.json({ jwt });
        
    } catch (error) {
        console.error('Login error:', error);
        res.status(500).json({ error: 'LOGIN_FAILED' });
    }
});

// 토큰 검증 (Game Server용)
router.post('/verify', async (req, res) => {
    try {
        const { jwt } = req.body;
        
        const result = await jwtService.verify(jwt);
        
        if (!result.valid) {
            return res.json({ valid: false, error: result.error });
        }
        
        // 밴 재확인
        const user = await db.query(`
            SELECT is_banned FROM auth_schema.users WHERE user_id = $1
        `, [result.user_id]);
        
        if (user.rows[0].is_banned) {
            return res.json({ valid: false, error: 'USER_BANNED' });
        }
        
        res.json({ valid: true, user_id: result.user_id });
        
    } catch (error) {
        console.error('Verify error:', error);
        res.status(500).json({ valid: false, error: 'VERIFY_FAILED' });
    }
});

module.exports = router;
```

---

### 3-3. Game Server (C++ Modular Monolith)

#### **책임**
- TCP 연결 관리
- 세션 관리 (인메모리)
- 채굴 시뮬레이션 (인메모리)
- 재화 관리 (DB 동기화)
- 미션 진행 추적
- game_schema만 접근

#### **모듈 구조**

```
game-server/
├── src/
│   ├── main.cpp                 # 진입점
│   ├── server/
│   │   ├── tcp_server.h/cpp     # Boost.asio TCP 서버
│   │   └── session_manager.h/cpp# 세션 관리
│   ├── modules/
│   │   ├── auth/
│   │   │   ├── auth_client.h/cpp      # Auth Service HTTP 클라이언트
│   │   │   └── jwt_verifier.h/cpp     # JWT 검증 (로컬 캐시)
│   │   ├── user/
│   │   │   ├── user_manager.h/cpp     # 유저 데이터 관리
│   │   │   └── economy_manager.h/cpp  # 재화 관리
│   │   ├── game/
│   │   │   ├── mining_manager.h/cpp   # 채굴 로직 (인메모리)
│   │   │   └── pickaxe_manager.h/cpp  # 곡괭이 관리
│   │   └── mission/
│   │       └── mission_manager.h/cpp  # 미션 관리
│   ├── db/
│   │   ├── connection.h/cpp     # PostgreSQL 연결
│   │   └── queries.h            # SQL 쿼리들
│   ├── protocol/
│   │   ├── packet.h/cpp         # 패킷 구조
│   │   └── message_handler.h/cpp# 메시지 핸들러
│   └── utils/
│       ├── logger.h/cpp         # spdlog 래퍼
│       └── timer.h/cpp          # 타이머 유틸
├── CMakeLists.txt
└── Dockerfile
```

#### **주요 클래스**

```cpp
// src/server/session_manager.h
class SessionManager {
public:
    // 세션 생성 (인증 완료 후)
    Session* CreateSession(TcpSocket* socket, const std::string& user_id);
    
    // 세션 조회
    Session* FindSession(const std::string& user_id);
    
    // 세션 제거
    void RemoveSession(const std::string& user_id);
    
    // 모든 세션에 브로드캐스트
    void BroadcastAll(const Packet& packet);
    
    // 틱 (1초마다 호출)
    void Tick();
    
private:
    std::unordered_map<std::string, Session*> sessions_;
    
    AuthClient auth_client_;
    UserManager user_manager_;
    MiningManager mining_manager_;
    MissionManager mission_manager_;
};
```

> **Session policy**: 기본값은 계정당 1세션·동일 IP 동시 접속 차단이나, 개발/부하 테스트 시 토글 가능한 서버 플래그(TBD)로 운영 모드를 전환한다. 라이브 시에는 차단 모드로 고정한다.

```cpp
// src/modules/game/mining_manager.h
class MiningManager {
public:
    // 채굴 시작
    bool StartMining(Session* session, int mineral_id);
    
    // 채굴 틱 (1초마다)
    void Tick(Session* session);
    
    // 채굴 완료 처리
    void CompleteMining(Session* session);
    
    // 스냅샷 저장 (5분마다)
    void SaveSnapshot(Session* session);
    
private:
    struct MiningState {
        int mineral_id;
        int64_t current_hp;
        int64_t max_hp;
        time_t start_time;
        bool dirty;  // DB 동기화 필요 여부
    };
    
    std::unordered_map<std::string, MiningState> mining_states_;
    DatabaseConnection* db_;
};
```

---

### 3-4. PostgreSQL

#### **책임**
- 영구 데이터 저장
- 트랜잭션 보장
- 복잡한 쿼리 처리

#### **스키마 구조**

```
infinite_pickaxe (Database)
├── auth_schema
│   ├── users              # 인증 정보
│   ├── jwt_families       # JWT 패밀리
│   ├── jwt_tokens         # 발급된 토큰
│   ├── session_history    # 세션 로그
│   └── ban_history        # 밴 이력
│
└── game_schema
    ├── user_game_data     # 재화, 진행도
    ├── pickaxe_slots      # 곡괭이 슬롯
    ├── mining_snapshots   # 채굴 스냅샷
    ├── mining_completions # 완료 기록
    ├── daily_missions     # 일일 미션
    └── critical_transactions  # 중요 트랜잭션
```

---

## 4. 데이터 흐름

### 4-1. 로그인 플로우

```
┌─────────┐                    ┌─────────┐                    ┌──────────┐
│ Client  │                    │  Auth   │                    │   DB     │
│ (Unity) │                    │ Service │                    │  (PG)    │
└────┬────┘                    └────┬────┘                    └────┬─────┘
     │                              │                              │
     │ 1. HTTPS POST /auth/login    │                              │
     │ { google_token: "..." }      │                              │
     ├─────────────────────────────►│                              │
     │                              │                              │
     │                              │ 2. Google API 검증           │
     │                              ├──────────────┐               │
     │                              │              │               │
     │                              │◄─────────────┘               │
     │                              │                              │
     │                              │ 3. INSERT/UPDATE users       │
     │                              ├─────────────────────────────►│
     │                              │                              │
     │                              │ 4. user_id, is_banned        │
     │                              │◄─────────────────────────────┤
     │                              │                              │
     │                              │ 5. INSERT jwt_families       │
     │                              ├─────────────────────────────►│
     │                              │                              │
     │                              │ 6. INSERT jwt_tokens         │
     │                              ├─────────────────────────────►│
     │                              │                              │
     │ 7. { jwt: "eyJ..." }         │                              │
     │◄─────────────────────────────┤                              │
     │                              │                              │
```

---

### 4-2. 게임 접속 플로우

```
┌─────────┐      ┌─────────┐      ┌─────────┐      ┌──────────┐
│ Client  │      │  Game   │      │  Auth   │      │   DB     │
│ (Unity) │      │ Server  │      │ Service │      │  (PG)    │
└────┬────┘      └────┬────┘      └────┬────┘      └────┬─────┘
     │                │                │                │
     │ 1. TCP Connect │                │                │
     ├───────────────►│                │                │
     │                │                │                │
     │ 2. ClientAuth  │                │                │
     │ { jwt: "..." } │                │                │
     ├───────────────►│                │                │
     │                │                │                │
     │                │ 3. POST /auth/verify            │
     │                │ { jwt: "..." } │                │
     │                ├───────────────►│                │
     │                │                │                │
     │                │                │ 4. SELECT jwt_tokens
     │                │                ├───────────────►│
     │                │                │                │
     │                │                │ 5. SELECT users
     │                │                │  (밴 체크)     │
     │                │                ├───────────────►│
     │                │                │                │
     │                │ 6. {valid: true,               │
     │                │    user_id: "..."}             │
     │                │◄───────────────┤                │
     │                │                │                │
     │                │ 7. SELECT user_game_data       │
     │                ├───────────────────────────────►│
     │                │                │                │
     │                │ 8. SELECT pickaxe_slots        │
     │                ├───────────────────────────────►│
     │                │                │                │
     │                │ 9. 세션 생성 (메모리)          │
     │                ├──────┐         │                │
     │                │      │         │                │
     │                │◄─────┘         │                │
     │                │                │                │
     │ 10. AuthResult │                │                │
     │ { user_data }  │                │                │
     │◄───────────────┤                │                │
     │                │                │                │
     │ 이후 auth_schema 접근 없음!      │                │
     │                │                │                │
```

---

### 4-3. 채굴 플로우

```
┌─────────┐                    ┌─────────┐                    ┌──────────┐
│ Client  │                    │  Game   │                    │   DB     │
│ (Unity) │                    │ Server  │                    │  (PG)    │
└────┬────┘                    └────┬────┘                    └────┬─────┘
     │                              │                              │
     │ 1. MiningStart { mineral_id: 3 }                           │
     ├─────────────────────────────►│                              │
     │                              │                              │
     │                              │ 2. 인메모리 상태 생성         │
     │                              │   mining_states_[user_id]    │
     │                              ├──────┐                       │
     │                              │      │                       │
     │                              │◄─────┘                       │
     │                              │                              │
     │ 3. MiningUpdate              │                              │
     │ { current_hp, max_hp }       │                              │
     │◄─────────────────────────────┤                              │
     │                              │                              │
     │     ... 1초 경과 ...         │                              │
     │                              │                              │
     │                              │ 4. Tick (1초마다)            │
     │                              │   HP -= DPS (메모리)         │
     │                              ├──────┐                       │
     │                              │      │                       │
     │                              │◄─────┘                       │
     │                              │                              │
     │ 5. MiningUpdate              │                              │
     │ { current_hp: 1362 }         │                              │
     │◄─────────────────────────────┤                              │
     │                              │                              │
     │     ... HP ≤ 0 ...           │                              │
     │                              │                              │
     │                              │ 6. BEGIN TRANSACTION         │
     │                              ├─────────────────────────────►│
     │                              │                              │
     │                              │ 7. UPDATE user_game_data     │
     │                              │    (gold += 140)             │
     │                              ├─────────────────────────────►│
     │                              │                              │
     │                              │ 8. INSERT mining_completions │
     │                              ├─────────────────────────────►│
     │                              │                              │
     │                              │ 9. COMMIT                    │
     │                              ├─────────────────────────────►│
     │                              │                              │
     │ 10. MiningComplete           │                              │
     │ { gold: 140 }                │                              │
     │◄─────────────────────────────┤                              │
     │                              │                              │
     │                              │ 11. 인메모리 상태 삭제        │
     │                              ├──────┐                       │
     │                              │      │                       │
     │                              │◄─────┘                       │
     │                              │                              │
```

---

### 4-4. 강화 플로우

```
┌─────────┐                    ┌─────────┐                    ┌──────────┐
│ Client  │                    │  Game   │                    │   DB     │
└────┬────┘                    └────┬────┘                    └────┬─────┘
     │                              │                              │
     │ 1. UpgradePickaxe            │                              │
     │ { slot_index: 0 }            │                              │
     ├─────────────────────────────►│                              │
     │                              │                              │
     │                              │ 2. 비용 계산 (메모리)         │
     │                              ├──────┐                       │
     │                              │      │                       │
     │                              │◄─────┘                       │
     │                              │                              │
     │                              │ 3. BEGIN TRANSACTION         │
     │                              ├─────────────────────────────►│
     │                              │                              │
     │                              │ 4. UPDATE user_game_data     │
     │                              │    SET gold = gold - 3500    │
     │                              │    WHERE gold >= 3500        │
     │                              ├─────────────────────────────►│
     │                              │                              │
     │                              │ 5. UPDATE pickaxe_slots      │
     │                              │    SET level = level + 1     │
     │                              ├─────────────────────────────►│
     │                              │                              │
     │                              │ 6. COMMIT                    │
     │                              ├─────────────────────────────►│
     │                              │                              │
     │                              │ 7. 메모리 캐시 업데이트       │
     │                              ├──────┐                       │
     │                              │      │                       │
     │                              │◄─────┘                       │
     │                              │                              │
     │ 8. UpgradeResult             │                              │
     │ { new_level: 13, new_dps }   │                              │
     │◄─────────────────────────────┤                              │
     │                              │                              │
```

---

## 5. 배포 전략

### 5-1. Docker Compose 구성

#### **docker-compose.yml**

```yaml
version: '3.8'

services:
  # Auth Service (NodeJS)
  auth-server:
    build: ./auth-server
    container_name: pickaxe_auth
    ports:
      - "10000:3000"
    environment:
      - NODE_ENV=production
      - PORT=3000
      - DB_HOST=postgres
      - DB_PORT=5432
      - DB_NAME=infinite_pickaxe
      - DB_USER=auth_user
      - DB_PASSWORD=${AUTH_DB_PASSWORD}
      - DB_SCHEMA=auth_schema
      - JWT_SECRET=${JWT_SECRET}
      - GOOGLE_CLIENT_ID=${GOOGLE_CLIENT_ID}
    volumes:
      - ./logs/auth:/app/logs
    depends_on:
      - postgres
    restart: unless-stopped
    networks:
      - pickaxe-network

  # Game Server (C++)
  game-server:
    build: ./game-server
    container_name: pickaxe_game
    ports:
      - "10001:8080"
    environment:
      - DB_HOST=postgres
      - DB_PORT=5432
      - DB_NAME=infinite_pickaxe
      - DB_USER=game_user
      - DB_PASSWORD=${GAME_DB_PASSWORD}
      - DB_SCHEMA=game_schema
      - AUTH_SERVICE_URL=http://auth-server:3000
      - LOG_LEVEL=info
    volumes:
      - ./logs/game:/app/logs
    depends_on:
      - postgres
      - auth-server
    restart: unless-stopped
    networks:
      - pickaxe-network

  # PostgreSQL
  postgres:
    image: postgres:15-alpine
    container_name: pickaxe_db
    ports:
      - "10002:5432"
    environment:
      - POSTGRES_DB=infinite_pickaxe
      - POSTGRES_USER=postgres
      - POSTGRES_PASSWORD=${DB_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./database/init.sql:/docker-entrypoint-initdb.d/00_init.sql
      - ./database/auth_schema:/docker-entrypoint-initdb.d/auth_schema
      - ./database/game_schema:/docker-entrypoint-initdb.d/game_schema
    restart: unless-stopped
    networks:
      - pickaxe-network
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  postgres_data:
    driver: local

networks:
  pickaxe-network:
    driver: bridge
```

---

#### **.env 파일**

```bash
# Database
DB_PASSWORD=your_super_secure_postgres_password
AUTH_DB_PASSWORD=your_super_secure_auth_password
GAME_DB_PASSWORD=your_super_secure_game_password

# JWT
JWT_SECRET=your_super_secret_jwt_key_min_32_chars_long

# Google OAuth
GOOGLE_CLIENT_ID=your_google_client_id.apps.googleusercontent.com
```

---

### 5-2. 배포 절차

#### **초기 배포**

```bash
# 1. 저장소 클론
git clone https://github.com/yourname/infinite-pickaxe.git
cd infinite-pickaxe

# 2. .env 파일 생성
cp .env.example .env
nano .env  # 환경 변수 설정

# 3. 빌드 및 실행
docker-compose build
docker-compose up -d

# 4. 로그 확인
docker-compose logs -f

# 5. 상태 확인
docker-compose ps
```

---

#### **업데이트 배포**

```bash
# 1. 코드 업데이트
git pull origin main

# 2. 재빌드 (변경된 서비스만)
docker-compose build auth-server  # 또는 game-server

# 3. 롤링 재시작
docker-compose up -d --no-deps auth-server

# 4. 로그 확인
docker-compose logs -f auth-server
```

---

#### **Zero Downtime 배포 (Phase 2)**

```bash
# 1. 새 버전 빌드
docker-compose build game-server

# 2. 스케일 업 (2개로)
docker-compose up -d --scale game-server=2

# 3. 헬스 체크 대기
sleep 10

# 4. 기존 인스턴스 제거
docker stop pickaxe_game_1

# 5. 확인 후 스케일 다운
docker-compose up -d --scale game-server=1
```

---

### 5-3. 홈서버 구성

#### **하드웨어 권장 사양**

| 컴포넌트 | 최소 | 권장 |
|----------|------|------|
| **CPU** | 4코어 | 8코어 |
| **RAM** | 8GB | 16GB |
| **스토리지** | 100GB SSD | 256GB SSD |
| **네트워크** | 100Mbps | 1Gbps |

---

#### **공유기 포트포워딩 설정**

```
외부 포트 → 내부 IP:포트
10000     → 192.168.0.100:10000  (Auth Service)
10001     → 192.168.0.100:10001  (Game Server)
10002     → 192.168.0.100:10002  (PostgreSQL - 개발용만)
```

**보안 주의**:
- PostgreSQL(10002)는 개발 중에만 외부 노출
- 프로덕션에서는 10000, 10001만 오픈

---

#### **DDNS 설정 (동적 IP 대응)**

```bash
# No-IP, DuckDNS 등 무료 DDNS 서비스 사용
# 예: your-game.ddns.net → 공유기 외부 IP
```

---

## 6. 네트워크 구성

### 6-1. 포트 할당 전략

| 포트 | 서비스 | 프로토콜 | 외부 노출 | 용도 |
|------|--------|----------|----------|------|
| **10000** | Auth Service | HTTPS | ✅ Yes | 로그인, JWT |
| **10001** | Game Server | TCP | ✅ Yes | 실시간 게임 |
| **10002** | PostgreSQL | TCP | ⚠️ 개발만 | DB 접근 |

---

### 6-2. 방화벽 규칙

```bash
# ufw 설정 (Ubuntu)
sudo ufw allow 10000/tcp  # Auth Service
sudo ufw allow 10001/tcp  # Game Server
# sudo ufw allow 10002/tcp  # PostgreSQL (개발만!)
sudo ufw enable
```

---

### 6-3. 네트워크 보안

#### **Auth Service (HTTPS)**

```javascript
// SSL/TLS 인증서 (Let's Encrypt)
const https = require('https');
const fs = require('fs');

const options = {
    key: fs.readFileSync('/etc/letsencrypt/live/your-domain/privkey.pem'),
    cert: fs.readFileSync('/etc/letsencrypt/live/your-domain/fullchain.pem')
};

https.createServer(options, app).listen(3000);
```

---

#### **Game Server (TCP)**

```cpp
// Phase 2: TCP TLS 적용
#include <boost/asio/ssl.hpp>

boost::asio::ssl::context ctx(boost::asio::ssl::context::tlsv13);
ctx.use_certificate_file("server.crt", boost::asio::ssl::context::pem);
ctx.use_private_key_file("server.key", boost::asio::ssl::context::pem);
```

---

## 7. 확장성 전략

### 7-1. 수직 확장 (Vertical Scaling)

**MVP 목표**: 동접 50-100명

```yaml
현재 리소스:
  CPU: 4코어
  RAM: 8GB
  
동접 500명 시:
  CPU: 8코어
  RAM: 16GB
  
동접 1000명 시:
  CPU: 16코어
  RAM: 32GB
```

**장점**:
- ✅ 구조 변경 없음
- ✅ 배포 간단

**한계**:
- ❌ 비용 증가 (비선형)
- ❌ 물리적 한계

---

### 7-2. 수평 확장 (Horizontal Scaling) - Phase 2

#### **Game Server 다중 인스턴스**

```yaml
services:
  game-server-1:
    build: ./game-server
    ports:
      - "10001:8080"
    
  game-server-2:
    build: ./game-server
    ports:
      - "10011:8080"
    
  game-server-3:
    build: ./game-server
    ports:
      - "10021:8080"
```

**로드 밸런서 (Nginx)**:

```nginx
upstream game_servers {
    least_conn;  # 최소 연결 수 기준
    server localhost:10001;
    server localhost:10011;
    server localhost:10021;
}

server {
    listen 10001;
    proxy_pass game_servers;
}
```

---

#### **세션 동기화 (Redis)**

```cpp
// Phase 2: Redis로 세션 백업
#include <redis++/redis++.h>

void SessionManager::SaveSession(Session* session) {
    redis_->set(
        "session:" + session->user_id,
        SerializeSession(session),
        std::chrono::seconds(3600)
    );
}

Session* SessionManager::LoadSession(const std::string& user_id) {
    auto data = redis_->get("session:" + user_id);
    if (data) {
        return DeserializeSession(*data);
    }
    return nullptr;
}
```

---

## 8. 모니터링 및 로깅

### 8-1. 로깅 전략

#### **Auth Service (NodeJS)**

```javascript
// src/utils/logger.js
const winston = require('winston');

const logger = winston.createLogger({
    level: 'info',
    format: winston.format.combine(
        winston.format.timestamp(),
        winston.format.json()
    ),
    transports: [
        new winston.transports.File({ 
            filename: '/app/logs/error.log', 
            level: 'error' 
        }),
        new winston.transports.File({ 
            filename: '/app/logs/combined.log' 
        })
    ]
});

// 사용 예시
logger.info('User logged in', { 
    user_id: userId, 
    ip: req.ip 
});
```

---

#### **Game Server (C++)**

```cpp
// src/utils/logger.cpp
#include <spdlog/spdlog.h>
#include <spdlog/sinks/rotating_file_sink.h>

class Logger {
public:
    static void Init() {
        // 10MB per file, 5 files max
        auto file_logger = spdlog::rotating_logger_mt(
            "game_logger",
            "/app/logs/game.log",
            1024 * 1024 * 10,
            5
        );
        
        spdlog::set_default_logger(file_logger);
        spdlog::set_level(spdlog::level::info);
        spdlog::set_pattern("[%Y-%m-%d %H:%M:%S] [%l] %v");
    }
};

// 사용 예시
spdlog::info("User {} started mining mineral {}", user_id, mineral_id);
spdlog::warn("Cheat detected: user {} DPS mismatch", user_id);
spdlog::error("DB connection failed: {}", error.what());
```

---

### 8-2. 로그 구조

```
logs/
├── auth/
│   ├── error.log           # 에러만
│   ├── combined.log        # 모든 로그
│   └── access.log          # HTTP 요청 (선택)
│
└── game/
    ├── game.log            # 일반 로그
    ├── transaction.log     # 트랜잭션 (채굴, 강화)
    ├── cheat.log           # 치트 의심 로그
    └── performance.log     # 성능 지표 (선택)
```

---

### 8-3. 모니터링 지표

#### **MVP (수동 모니터링)**

```bash
# CPU/메모리 사용률
docker stats

# 로그 실시간 확인
tail -f logs/game/game.log

# DB 연결 수
psql -U postgres -d infinite_pickaxe -c "
    SELECT count(*) FROM pg_stat_activity 
    WHERE datname = 'infinite_pickaxe';
"

# 현재 동접
psql -U postgres -d infinite_pickaxe -c "
    SELECT COUNT(*) FROM game_schema.user_game_data 
    WHERE updated_at > NOW() - INTERVAL '5 minutes';
"
```

---

#### **Phase 2 (자동 모니터링)**

**Prometheus + Grafana**:

```yaml
# docker-compose.yml 추가
services:
  prometheus:
    image: prom/prometheus
    ports:
      - "9090:9090"
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
  
  grafana:
    image: grafana/grafana
    ports:
      - "3000:3000"
    depends_on:
      - prometheus
```

**메트릭 수집**:
- 동접자 수 (CCU)
- 초당 패킷 수 (PPS)
- DB 쿼리 시간
- CPU/메모리 사용률
- 에러율

---

## 9. 보안 아키텍처

### 9-1. 인증/인가 흐름

```
┌──────────────────────────────────────────────────┐
│          보안 레이어                             │
├──────────────────────────────────────────────────┤
│ 1. Google Play Games 인증                        │
│    └─ 신뢰할 수 있는 제3자 인증                   │
│                                                  │
│ 2. JWT 발급 (Auth Service)                       │
│    └─ HS256 서명, 7일 유효                       │
│                                                  │
│ 3. JWT 검증 (Game Server)                        │
│    └─ Auth Service API 호출 (1회)                │
│                                                  │
│ 4. 세션 기반 통신 (TCP)                          │
│    └─ 인증 후 user_id로 식별                     │
│                                                  │
│ 5. 서버 권위 검증                                │
│    └─ 모든 액션 서버에서 검증                    │
└──────────────────────────────────────────────────┘
```

---

### 9-2. 치트 방지

#### **DPS 검증**

```cpp
bool MiningManager::ValidateMiningProgress(Session* session) {
    auto& state = mining_states_[session->user_id];
    
    // 서버 기준 계산
    auto elapsed = GetCurrentTime() - state.start_time;
    auto expected_hp = state.max_hp - (session->total_dps * elapsed);
    
    // 클라이언트 보고 HP와 비교
    auto diff = std::abs(expected_hp - state.current_hp);
    auto tolerance = expected_hp * 0.1f;  // 10% 허용
    
    if (diff > tolerance) {
        session->cheat_score++;
        
        spdlog::warn("DPS mismatch detected: user={} diff={} expected={} actual={}",
            session->user_id, diff, expected_hp, state.current_hp);
        
        if (session->cheat_score >= 10) {
            BanUser(session->user_id, "Automated cheat detection");
            return false;
        }
    }
    
    return true;
}
```

---

#### **Rate Limiting**

```javascript
// Auth Service
const rateLimit = require('express-rate-limit');

const loginLimiter = rateLimit({
    windowMs: 15 * 60 * 1000,  // 15분
    max: 5,  // 최대 5회
    message: 'Too many login attempts, please try again later.'
});

app.post('/auth/login', loginLimiter, async (req, res) => {
    // ...
});
```

```cpp
// Game Server
class RateLimiter {
public:
    bool CheckLimit(const std::string& user_id, uint16_t msg_type) {
        auto key = user_id + ":" + std::to_string(msg_type);
        auto& limit = limits_[msg_type];
        
        auto now = GetCurrentTime();
        auto& counter = counters_[key];
        
        // 윈도우 리셋
        if (now - counter.window_start > limit.window_seconds) {
            counter.count = 0;
            counter.window_start = now;
        }
        
        counter.count++;
        
        if (counter.count > limit.max_count) {
            spdlog::warn("Rate limit exceeded: user={} msg_type={}",
                user_id, msg_type);
            return false;
        }
        
        return true;
    }
    
private:
    struct Limit {
        int max_count;
        int window_seconds;
    };
    
    std::map<uint16_t, Limit> limits_ = {
        {0x0100, {10, 1}},    // MiningStart: 초당 10회
        {0x0200, {1, 1}},     // Upgrade: 초당 1회
        {0x0300, {10, 60}},   // MissionClaim: 분당 10회
    };
    
    struct Counter {
        int count;
        time_t window_start;
    };
    
    std::map<std::string, Counter> counters_;
};
```

---

### 9-3. SQL Injection 방어

```cpp
// ✅ 좋은 예: Prepared Statement
pqxx::work txn(conn_);
auto result = txn.exec_params(
    "UPDATE user_game_data SET gold = gold + $1 WHERE user_id = $2",
    gold_amount,
    user_id
);

// ❌ 나쁜 예: 문자열 연결
auto query = "UPDATE user_game_data SET gold = gold + " + 
             std::to_string(gold_amount) + 
             " WHERE user_id = '" + user_id + "'";
// SQL Injection 취약!
```

---

## 10. 장애 대응

### 10-1. 장애 시나리오

#### **시나리오 1: Game Server 다운**

```
현상:
- 모든 클라이언트 연결 끊김
- 진행 중 채굴 손실 가능

대응:
1. 자동 재시작 (Docker restart policy)
2. 채굴 스냅샷 복구 (5분 내)
3. 유저에게 보상 지급

예방:
- 정기 스냅샷 (5분)
- 헬스 체크 (30초)
```

---

#### **시나리오 2: PostgreSQL 다운**

```
현상:
- 로그인 불가
- 채굴 완료 저장 실패
- 재화 변동 손실

대응:
1. DB 재시작
2. 백업에서 복구
3. 트랜잭션 로그로 보상

예방:
- 일일 백업
- WAL 아카이빙
- 레플리케이션 (Phase 2)
```

---

#### **시나리오 3: Auth Service 다운**

```
현상:
- 신규 로그인 불가
- 기존 세션은 정상 (JWT 유효)

대응:
1. 자동 재시작
2. 로그 확인
3. 필요시 스케일 업

예방:
- 헬스 체크
- 다중 인스턴스 (Phase 2)
```

---

### 10-2. 헬스 체크

```yaml
# docker-compose.yml
services:
  game-server:
    healthcheck:
      test: ["CMD", "/app/healthcheck"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s
```

```cpp
// healthcheck.cpp
#include <boost/asio.hpp>

int main() {
    try {
        boost::asio::io_context io;
        tcp::socket socket(io);
        
        // localhost:8080 연결 시도
        socket.connect(tcp::endpoint(
            boost::asio::ip::address::from_string("127.0.0.1"), 
            8080
        ));
        
        // 간단한 핑 패킷 전송
        // ...
        
        return 0;  // 성공
    } catch (std::exception& e) {
        return 1;  // 실패
    }
}
```

---

## 11. 성능 목표

### 11-1. MVP 목표 (동접 50-100명)

| 지표 | 목표 | 측정 방법 |
|------|------|----------|
| **로그인 시간** | < 2초 | Auth Service 응답 시간 |
| **게임 접속** | < 3초 | TCP 연결 + 데이터 로드 |
| **패킷 레이턴시** | < 100ms | Round-trip time |
| **채굴 틱 정확도** | ±100ms | 서버 틱 vs 기대 시간 |
| **DB 쿼리 시간** | < 50ms | 평균 쿼리 시간 |
| **CPU 사용률** | < 50% | Docker stats |
| **메모리 사용률** | < 4GB | Docker stats |

---

### 11-2. 스트레스 테스트 (Phase 2)

```python
# load_test.py
import asyncio
import aiohttp
from faker import Faker

fake = Faker()

async def simulate_user():
    async with aiohttp.ClientSession() as session:
        # 1. 로그인
        jwt = await login(session)
        
        # 2. 게임 접속
        tcp_socket = await connect_game_server(jwt)
        
        # 3. 채굴 10회
        for _ in range(10):
            await mining_cycle(tcp_socket)
        
        # 4. 종료
        await tcp_socket.close()

async def main():
    # 동시 100명 시뮬레이션
    tasks = [simulate_user() for _ in range(100)]
    await asyncio.gather(*tasks)

if __name__ == '__main__':
    asyncio.run(main())
```

---

## 12. Phase별 진화 계획

### 12-1. Phase 1 - MVP (현재)

**목표**: 빠른 검증, 30-50명 테스트

```yaml
아키텍처:
  - Auth Service (NodeJS)
  - Game Server (C++ Modular)
  - PostgreSQL (스키마 분리)
  - Docker Compose
  
기능:
  - 로그인 (Google Play)
  - 채굴 (7개 광물)
  - 강화 (Lv 0-19)
  - 일일 미션
  - 슬롯 시스템
  
인프라:
  - 홈서버 온프레미스
  - 수동 배포
  - 파일 로깅
  
기간: 2-3개월
```

---

### 12-2. Phase 2 - 성장 (동접 500-1000명)

**트리거**: MVP 성공, 유저 증가

```yaml
추가/변경:
  - Redis (세션 백업)
  - Message Queue (RabbitMQ)
  - Analytics Service (분리)
  - Prometheus + Grafana
  - Game Server 수평 확장
  - HTTPS/TLS 전면 적용
  
기능:
  - 주간 미션
  - 업적 시스템
  - 실제 IAP
  - 랭킹 (비실시간)
  
인프라:
  - VPS로 이전 고려
  - CI/CD (GitHub Actions)
  - 자동 백업
  
기간: 3-6개월
```

---

### 12-3. Phase 3 - 확장 (동접 5000+)

**트리거**: 지속적 성장, 수익 안정

```yaml
전환:
  - MSA 전환 (점진적)
  - Kubernetes
  - Service Mesh (Istio)
  - 물리 DB 분리
  
서비스:
  - Auth Service
  - User Service (분리)
  - Game Service
  - Mission Service (분리)
  - Economy Service (분리)
  - Analytics Service
  
기능:
  - PvP 토너먼트
  - 길드 시스템
  - 실시간 랭킹
  - 시즌제
  
인프라:
  - AWS/GCP 클라우드
  - 로드 밸런서
  - CDN
  - 다지역 배포
  
기간: 6-12개월
```

---

## 13. 문서 변경 이력

| 버전 | 날짜 | 변경 내용 |
|------|------|----------|
| 1.0 | 2024-12-08 | 초안 작성 (MVP 아키텍처) |

---

## 14. 참고 자료

### 14-1. 외부 문서

- [PostgreSQL 공식 문서](https://www.postgresql.org/docs/)
- [Boost.asio 공식 문서](https://www.boost.org/doc/libs/1_83_0/doc/html/boost_asio.html)
- [Docker Compose 공식 문서](https://docs.docker.com/compose/)
- [Unity 네트워킹 가이드](https://docs.unity3d.com/Manual/UNetOverview.html)

### 14-2. 내부 문서

- [GDD] 무한의 곡괭이 게임 디자인 문서
- [TDD] 네트워크 프로토콜 설계
- [TDD] 데이터베이스 스키마 설계

---

## 15. 요약

### ✅ 핵심 아키텍처 원칙

```
1. Modular Monolith
   └─ 간단하게 시작, 필요시 MSA 전환
   
2. 스키마 분리
   └─ auth_schema, game_schema (물리 1개)
   
3. 인메모리 우선
   └─ 세션, 채굴 진행도는 메모리 관리
   
4. 서버 권위
   └─ 모든 로직은 서버에서 검증
   
5. 파일 로깅
   └─ 일반 로그는 파일, 중요 TX만 DB
   
6. Docker Compose
   └─ 간편한 배포, 일관된 환경
   
7. 점진적 확장
   └─ Phase별 진화 (MVP → 성장 → 확장)
```

---

**문서 끝**
