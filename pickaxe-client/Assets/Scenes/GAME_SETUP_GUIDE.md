# Game 씬 탭 시스템 설정 가이드

Game.unity 씬에서 탭 전환 시스템을 설정하는 방법을 단계별로 안내합니다.

## 1. GameTabManager GameObject 생성 및 설정

### 1-1. GameObject 생성
1. Unity Editor에서 `Game.unity` 씬을 엽니다
2. Hierarchy 창에서 우클릭 → `Create Empty` 선택
3. 생성된 GameObject 이름을 `GameTabManager`로 변경

### 1-2. GameTabManager 컴포넌트 추가
1. `GameTabManager` GameObject 선택
2. Inspector 창에서 `Add Component` 클릭
3. `GameTabManager` 검색 후 선택

## 2. 탭 GameObject 생성

현재 `MiningTab`만 존재하므로, 나머지 4개 탭 GameObject를 생성해야 합니다.

### 2-1. 탭 GameObject 구조 파악
먼저 기존 `MiningTab`의 위치를 확인합니다:
- Hierarchy 창에서 `MiningTab` GameObject를 찾습니다
- 부모 GameObject를 확인합니다 (예: Canvas 또는 GamePanel 등)

### 2-2. 나머지 탭 GameObject 생성
`MiningTab`과 같은 부모 아래에 다음 GameObject들을 생성합니다:

1. **UpgradeTab** 생성
   - `MiningTab`의 부모 GameObject에서 우클릭 → `Create Empty`
   - 이름을 `UpgradeTab`으로 변경

2. **QuestTab** 생성
   - 동일한 방법으로 `QuestTab` 생성

3. **ShopTab** 생성
   - 동일한 방법으로 `ShopTab` 생성

4. **SettingsTab** 생성
   - 동일한 방법으로 `SettingsTab` 생성

### 2-3. 탭별 컨트롤러 컴포넌트 추가

각 탭 GameObject에 해당 컨트롤러를 추가합니다:

1. `MiningTab` GameObject 선택 → `Add Component` → `MiningTabController`
2. `UpgradeTab` GameObject 선택 → `Add Component` → `UpgradeTabController`
3. `QuestTab` GameObject 선택 → `Add Component` → `QuestTabController`
4. `ShopTab` GameObject 선택 → `Add Component` → `ShopTabController`
5. `SettingsTab` GameObject 선택 → `Add Component` → `SettingsTabController`

### 2-4. 초기 활성화 상태 설정
1. `MiningTab`만 활성화 상태로 둡니다 (체크박스 ✓)
2. 나머지 탭들은 비활성화합니다 (체크박스 해제)

## 3. GameTabManager 참조 연결

`GameTabManager` GameObject를 선택하고 Inspector에서 다음 참조들을 연결합니다.

### 3-1. Tabs 섹션
다음 GameObject들을 드래그 앤 드롭으로 연결:
- **Mining Tab**: `MiningTab` GameObject
- **Upgrade Tab**: `UpgradeTab` GameObject
- **Quest Tab**: `QuestTab` GameObject
- **Shop Tab**: `ShopTab` GameObject
- **Settings Tab**: `SettingsTab` GameObject

### 3-2. Footer Buttons 섹션
FooterBar에서 각 버튼을 찾아 연결:
- **Mine Button**: `FooterBar/MineButton` GameObject의 Button 컴포넌트
- **Upgrade Button**: `FooterBar/UpgradeButton` GameObject의 Button 컴포넌트
- **Quest Button**: `FooterBar/QuestButton` GameObject의 Button 컴포넌트
- **Shop Button**: `FooterBar/ShopButton` GameObject의 Button 컴포넌트
- **Settings Button**: `FooterBar/SettingsButton` GameObject의 Button 컴포넌트

> **참고**: Button 컴포넌트를 직접 연결해야 합니다. GameObject를 드래그하면 자동으로 Button 컴포넌트가 선택됩니다.

## 4. 각 탭 컨트롤러 UI 참조 연결

각 탭 컨트롤러도 UI 요소들을 연결해야 합니다. 현재는 UI GameObject가 없으므로, 추후 UI 구현 시 다음과 같이 연결하면 됩니다:

### 4-1. MiningTabController
- **Mine Info Text**: 광물 이름 표시 TextMeshProUGUI
- **Mine HP Slider**: HP 바 Slider
- **Mine HP Text**: HP 텍스트 TextMeshProUGUI
- **DPS Text**: DPS 표시 TextMeshProUGUI
- **Select Mineral Button**: 광물 선택 Button

### 4-2. UpgradeTabController
- **Pickaxe Level Text**: 곡괭이 레벨 표시
- **Current DPS Text**: 현재 DPS 표시
- **Next DPS Text**: 다음 DPS 표시
- **Upgrade Cost Text**: 강화 비용 표시
- **Upgrade Button**: 강화 버튼
- **Ad Discount Button**: 광고 할인 버튼

### 4-3. QuestTabController
- **Quest Count Text**: 미션 완료 개수 표시
- **Quest List Container**: 미션 목록 컨테이너
- **Quest Item Prefab**: 미션 아이템 프리팹
- **Refresh Quest Button**: 미션 재설정 버튼
- **Refresh Count Text**: 재설정 횟수 표시
- **Milestone 3/5/7 Text**: 마일스톤 텍스트들

### 4-4. ShopTabController
- **Watch Ad Buttons (1-3)**: 광고 시청 버튼들
- **Ad Count Text**: 광고 시청 횟수 표시
- **Unlock Slot Buttons (2-4)**: 슬롯 해금 버튼들
- **Slot Cost Texts (2-4)**: 슬롯 비용 텍스트들
- **IAP Buttons (Small/Medium/Large)**: IAP 구매 버튼들

### 4-5. SettingsTabController
- **BGM Slider**: BGM 볼륨 슬라이더
- **SFX Slider**: 효과음 볼륨 슬라이더
- **BGM Volume Text**: BGM 볼륨 텍스트
- **SFX Volume Text**: 효과음 볼륨 텍스트
- **Offline Notification Toggle**: 오프라인 알림 토글
- **Mission Notification Toggle**: 미션 알림 토글
- **Account Info Text**: 계정 정보 텍스트
- **Logout Button**: 로그아웃 버튼
- **Version Text**: 버전 정보 텍스트
- **Terms/Privacy/Support Buttons**: 약관/개인정보/고객지원 버튼들

## 5. 테스트

### 5-1. 기본 동작 테스트
1. Unity Editor에서 Play 버튼 클릭
2. FooterBar의 각 버튼을 클릭하여 탭이 전환되는지 확인
3. 한 번에 하나의 탭만 활성화되어야 합니다

### 5-2. 디버그 로그 확인
콘솔 창에서 다음과 같은 로그를 확인할 수 있습니다:
```
GameTabManager: 탭 전환 - Mining → Upgrade
UpgradeTabController: 탭 활성화됨
```

### 5-3. 디버그 로그 토글
디버그 로그를 끄려면 각 스크립트 상단의 define을 주석 처리:
```csharp
// #define DEBUG_TAB_MANAGER  // 이렇게 주석 처리
```

## 6. 예상 Hierarchy 구조

```
Game
├── Canvas
│   ├── GamePanel (또는 다른 부모 GameObject)
│   │   ├── MiningTab (활성화 ✓)
│   │   │   └── MiningTabController
│   │   ├── UpgradeTab (비활성화)
│   │   │   └── UpgradeTabController
│   │   ├── QuestTab (비활성화)
│   │   │   └── QuestTabController
│   │   ├── ShopTab (비활성화)
│   │   │   └── ShopTabController
│   │   └── SettingsTab (비활성화)
│   │       └── SettingsTabController
│   └── FooterBar
│       ├── MineButton (Button)
│       ├── UpgradeButton (Button)
│       ├── QuestButton (Button)
│       ├── ShopButton (Button)
│       └── SettingsButton (Button)
└── GameTabManager
    └── GameTabManager (컴포넌트)
```

## 7. 문제 해결

### 문제: 버튼 클릭 시 탭이 전환되지 않음
- GameTabManager의 모든 참조가 올바르게 연결되었는지 확인
- FooterBar 버튼들이 Button 컴포넌트를 가지고 있는지 확인
- 콘솔에서 에러 메시지 확인

### 문제: 여러 탭이 동시에 표시됨
- 초기 상태에서 MiningTab만 활성화하고 나머지는 비활성화했는지 확인
- GameTabManager의 Awake()가 호출되는지 확인 (콘솔 로그)

### 문제: 탭 컨트롤러 스크립트를 찾을 수 없음
- Unity Editor를 재시작하여 스크립트를 다시 컴파일
- `.meta` 파일이 올바르게 생성되었는지 확인
- 스크립트 파일이 올바른 경로에 있는지 확인:
  - `Assets/Scripts/Runtime/UI/Game/`

## 8. 다음 단계

1. **UI 요소 추가**: 각 탭에 실제 UI 요소들을 추가
2. **서버 연동**: NetworkManager와 연결하여 서버 데이터 표시
3. **애니메이션**: 탭 전환 시 페이드 인/아웃 효과 추가
4. **사운드**: 버튼 클릭 효과음 추가

---

**작성일**: 2025-12-12
**작성자**: Claude Code Assistant
