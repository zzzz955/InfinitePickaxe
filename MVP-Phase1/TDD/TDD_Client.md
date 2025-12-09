# Infinite Pickaxe - Client TDD (MVP)

문서 버전: 0.1 (초안)  
대상 에디터: Unity 2022.3 LTS (2022.3.62f2)  
플랫폼: Android 우선, 추후 iOS/PC 확장

---

## 1. 목표와 범위
- MVP에서 필요한 모든 서버 API를 UI 플로우로 검증한다.
- 프로토콜 변경 없이 클라이언트가 서버와 상호 운용 가능함을 보장한다.
- 네트워크 오류, 세션 만료, 재시도 시나리오를 최소 커버한다.

## 2. 기술 스택 및 의존성
- Unity 2022.3 LTS, IL2CPP / Mono 플레이 모드.
- Google.Protobuf 패키지, 프로토 코드: `protocol/game.proto`를 C#으로 생성하여 `Assets/Scripts/Generated/`에 포함.
- 인증: Google Play Games SDK 1.x(계정 선택 가능) 사용. RefreshToken은 SecureStorage(Android Keystore 등) 우선.
- 전송 방식: TCP 길이 프리픽스(4바이트 Little Endian) + Envelope(proto).
- 환경 설정: ScriptableObject 또는 `Resources/config.json`(dev/stage/prod 호스트, 포트, 타임아웃, 하트비트 주기).

## 3. 아키텍처 개요
- Bootstrap 씬: DI 컨테이너/Service Locator 생성, 네트워크 매니저 초기화, 설정 로드, 메타데이터/버전 체크, SecureStorage에서 RefreshToken 조회.
- MainScene: 로그인 진입점. RefreshToken 존재 시 자동 재인증→유저 데이터 로드 후 GameScene으로 즉시 전환. 미존재 시 GPG 로그인 플로우 노출.
- GameScene: MVP 기준 단일 씬으로 플레이 진행(향후 Zone 추가 시 Scene 분리 검토). 씬 전환 시 LoadingOverlay 표시.
- 네트워크 계층: `TcpClient`(길이 프리픽스), 송신 큐, 수신 디멀티플렉서(Envelope.msg_type 기반 라우팅).
- 데이터 계층: 최소 캐시(현재 골드, 슬롯 상태, 미션 상태, 오프라인 결과). 저장소는 메모리 우선, RefreshToken은 SecureStorage, 나머지 로컬 캐시는 PlayerPrefs/파일은 Dev 전용.
- 전역 시스템: DontDestroyOnLoad 싱글톤/서비스로 네트워크, 세션, 설정, 메타데이터 보관(패턴 확정 필요).

## 4. 프로토콜 적용 규칙
- Envelope.version은 서버와 동일(현재 1). 불일치 시 핸드셰이크 단계에서 오류 노출.
- seq/timestamp 검증: 클라에서 seq 증가(핸드셰이크 후 1부터), timestamp는 Unix 초. 서버로부터 INVALID_SEQUENCE/INVALID_TIMESTAMP 수신 시 재동기화 팝업 및 재연결 옵션 제공.
- 하트비트: 30초 간격 송신, Ack 수신 실패 시 2회 재시도 후 재연결.
- 메시지 직렬화: proto 바이너리를 payload에 넣고 msg_type 문자열로 구분.

## 5. 화면/플로우별 요구사항
1) 부트스트랩  
   - 설정/메타 로드 실패 시 기본 localhost:10001(dev)로 연결 시도 여부 팝업.  
   - 프로토 버전/앱 버전 로그 남김.  
   - SecureStorage에서 RefreshToken 조회.
2) 로그인/핸드셰이크  
   - RefreshToken 존재 시 자동 재인증→유저 데이터 로드 후 GameScene 이동.  
   - 미존재/실패 시 GPG 로그인 버튼 노출(계정 선택 이슈로 SDK 버전 별 플로우 테스트 필요).  
   - 성공 시 스냅샷(UserDataSnapshot) UI에 반영: 골드, 크리스탈, 슬롯 잠금 여부, 현재 광물, HP, DPS, 최고 레벨.  
   - 실패 코드(1001 AUTH_FAILED, 1003 EXPIRED 등) 표시 및 재시도 버튼.
3) 하트비트  
   - 상태 표시(연결, 지연시간 추정).  
   - 2회 연속 실패 시 자동 재연결 트리거.
4) 채굴(Stub 가능)  
   - MINE_START/MINE_SYNC 송신, 서버 MiningUpdate/MiningComplete 수신을 UI에 반영.  
   - 메인 탭이 아닐 때도 재화/HP 변화가 상단 UI에 반영되도록 전역 상태 갱신. HP 바를 항상 노출할지 여부는 추후 결정.  
   - 스텁 단계: 서버 응답만 표시. 추후 실제 HP 바/데미지 합산 로직 연계.
5) 강화  
   - UpgradePickaxe 전송: 슬롯 선택, 목표 레벨 입력(UI는 +1 고정으로 제한).  
   - UpgradeResult 표시: 성공/실패, 사용 골드, 남은 골드, 확률(basis 10000), pity 누적.  
   - 에러 코드: 3001(INDEX/골드 부족), 3002(잘못된 목표 레벨), 3004(슬롯 없음) 등 메시지 표기.
6) 슬롯 해금  
   - SlotUnlock -> SlotUnlockResult 처리, 크리스탈 차감 반영.
7) 미션  
   - MissionUpdate 렌더링, Claim/Reroll 처리, 보상 반영.
8) 오프라인 보상  
   - OfflineRewardRequest/OfflineReward 수신 후 결과 표시, 골드 반영.
9) 에러/알림  
   - Error(msg_type=ERROR) 수신 시 토스트/다이얼로그, 코드와 메시지 표시.

## 6. 네트워크 상태/예외 처리
- 연결 실패: 즉시 UI 알림, 3초 지연 후 재시도 옵션.
- 송신 큐: 비동기 실패 시 큐 잔여물을 폐기하고 재연결 후 핸드셰이크부터 재개.
- 타임아웃: 요청-응답 기대형 메시지(Upgrade, Slot, Mission) 5초 타임아웃, 미수신 시 사용자에게 재시도 제안.
- 세션 만료: 1003 수신 시 JWT 재입력 또는 재발급 경고 후 재연결.

## 7. 로깅 및 디버깅
- 에디터: 콘솔에 전송/수신 msg_type, seq, 길이, 주요 필드 로그(개인정보 최소화).
- 디바이스: 최소 수준의 info 로그 + 오류 로그. 네트워크 덤프는 개발 빌드 한정 옵션.
- 설정으로 로그 레벨(dev/prod) 전환 가능.

## 8. 테스트 케이스(요약)
- 연결/핸드셰이크: 정상 JWT, 만료 JWT, 잘못된 호스트/포트.
- 하트비트: 네트워크 단절/복구, 서버 Ack 미수신.
- 강화: 성공/실패, 골드 부족, 잘못된 레벨, pity 증가/초기화 확인.
- 채굴: MiningComplete 수신 시 골드/HP UI 반영 확인(Stub 단계 OK).
- 슬롯 해금: 크리스탈 부족/성공 케이스.
- 미션: Claim 후 상태 갱신, Reroll 처리.
- 오프라인 보상: 정상 응답 UI 반영.
- 에러 처리: INVALID_SEQUENCE, TIMESTAMP_MISMATCH 수신 시 재연결 유도.

## 9. 빌드/배포
- Dev/Stage/Prod 설정 분리(ScriptableObject 또는 json).  
- Android 개발 빌드: ARM64, IL2CPP, 디버그 심볼 포함.  
- 에디터 플레이 자동화 스크립트: 핸드셰이크 → 하트비트 → Upgrade 한 번 호출까지 단축키로 실행.

## 10. 추후 결정/분기 필요 항목
- JWT 입력 방식: 테스트용 수동 입력 vs 인증 플로우 연동(선택 필요).
- 로딩/씬 전환 패턴: 단일 씬 + UI 스택 vs 멀티 씬 additive(선택 가능).  
- 채굴 시뮬레이션을 클라이언트에서 어느 수준까지 계산해 보여줄지(Stub → 실제 스펙 반영 시점 협의).
