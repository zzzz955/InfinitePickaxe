# Protocol (protobuf, MVP)

공통 `.proto` 스키마를 관리합니다. C++/C# 양쪽에서 같은 파일을 코드 생성해 사용합니다.

- 전송: TCP length-prefix(4바이트) + `Envelope` protobuf 메시지
- 메시지: MVP 기준 (Handshake, Heartbeat, Mining*, Upgrade, Mission, SlotUnlock, OfflineReward, Gem*, Error)

## 메시지 카테고리

### 접속/세션 (1-9)
- Handshake, HandshakeResult
- Heartbeat, HeartbeatAck
- UserDataSnapshot

### 광물 (20-29)
- MineralListRequest/Response
- ChangeMineralRequest/Response

### 채굴 (30-39)
- MiningUpdate (서버 → 클라이언트, 40ms 틱)
- MiningComplete

### 슬롯 (40-49)
- AllSlotsRequest/Response
- SlotUnlock, SlotUnlockResult

### 강화 (50-59)
- UpgradeRequest, UpgradeResult

### 미션 (60-69)
- DailyMissionsRequest/Response
- MissionProgressUpdate
- MissionComplete, MissionCompleteResult
- MissionReroll, MissionRerollResult
- MilestoneClaim, MilestoneClaimResult
- MilestoneState

### 광고 (70-79)
- AdWatchComplete, AdWatchResult
- AdCountersState

### 재화 (80-89)
- CurrencyUpdate

### 오프라인 보상 (90-99)
- OfflineRewardRequest, OfflineRewardResult

### 에러 (100)
- ErrorNotification

### 보석 시스템 (110-127)
- GemListRequest/Response (보석 목록 조회)
- GemGachaRequest/Result (가챠 뽑기)
- GemSynthesisRequest/Result (합성)
- GemConversionRequest/Result (타입 변환)
- GemDiscardRequest/Result (분해)
- GemEquipRequest/Result (장착)
- GemUnequipRequest/Result (해제)
- GemSlotUnlockRequest/Result (보석 슬롯 해금)
- GemInventoryExpandRequest/Result (인벤토리 확장)

## Enum 타입

### GemType
- ATTACK_SPEED (공격 속도)
- CRIT_RATE (크리티컬 확률)
- CRIT_DMG (크리티컬 데미지)

### GemGrade
- COMMON (일반)
- RARE (고급)
- EPIC (희귀)
- HERO (영웅)
- LEGENDARY (전설)

## 인증 서버 부트스트랩
- stateless HTTP/JSON API는 `auth_api.md`에 명세
