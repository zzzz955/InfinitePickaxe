# Game 씬 탭 UI 생성 가이드

GDD 6. UI/UX 명세를 기반으로 각 탭의 GameObject 구조를 정리한 가이드입니다.

## 공통 구조

모든 탭은 다음 공통 구조를 따릅니다:

```
[TabName] (Root GameObject)
├── Component: RectTransform
├── Component: VerticalLayoutGroup
└── Component: [TabName]Controller
```

**RectTransform 기본 설정:**
- Anchor: Min(0, 0.5), Max(1, 0.5)
- Anchored Position: (0, 48)
- Size Delta: (0, 1632)
- Pivot: (0.5, 0.5)

**VerticalLayoutGroup 기본 설정:**
- Padding: Left/Right 40, Top/Bottom 40
- Spacing: 30
- Child Alignment: Upper Center (1)
- Child Force Expand: Width/Height 모두 Off

---

## 1. UpgradeTab (강화 탭)

### GDD 참조
**섹션 6-3**: 강화 탭 와이어프레임

### GameObject 구조

```
UpgradeTab (Root)
├── TitleText (TextMeshProUGUI)
│   └── Text: "⛏️ 곡괭이 강화"
│   └── Font Size: 60, Bold, Center
│   └── Size: (600, 80)
│
├── PickaxeArea (Empty GameObject)
│   └── PickaxeImage (Image) [추후 스프라이트 추가]
│   └── Size: (200, 200)
│
├── PickaxeLevelText (TextMeshProUGUI)
│   └── Text: "곡괭이 레벨: 0"
│   └── Font Size: 48, Center
│   └── Size: (500, 60)
│
├── CurrentDPSText (TextMeshProUGUI)
│   └── Text: "현재 DPS: 10"
│   └── Font Size: 40, Center
│   └── Size: (500, 50)
│
├── NextDPSText (TextMeshProUGUI)
│   └── Text: "다음 DPS: 17 (+70%)"
│   └── Font Size: 40, Center
│   └── Color: Green (0.2, 1, 0.2)
│   └── Size: (500, 50)
│
├── UpgradeCostText (TextMeshProUGUI)
│   └── Text: "강화 비용: 💰 5"
│   └── Font Size: 40, Center
│   └── Color: Gold (1, 0.8, 0.2)
│   └── Size: (500, 50)
│
├── UpgradeButton (Button)
│   ├── RectTransform: Size (500, 120)
│   ├── Image: Color Green (0.2, 0.8, 0.2)
│   └── Text (Child)
│       └── Text: "강화하기"
│       └── Font Size: 48, Bold, Center
│       └── Color: Black
│
└── AdDiscountButton (Button)
    ├── RectTransform: Size (500, 100)
    ├── Image: Color Purple (0.8, 0.2, 0.8)
    └── Text (Child)
        └── Text: "📺 광고 보고 -25% (0/3)"
        └── Font Size: 36, Center
        └── Color: White
```

### UpgradeTabController 연결

Inspector에서 다음 SerializeField 연결:
- `pickaxeLevelText` → PickaxeLevelText
- `currentDPSText` → CurrentDPSText
- `nextDPSText` → NextDPSText
- `upgradeCostText` → UpgradeCostText
- `upgradeButton` → UpgradeButton (Button 컴포넌트)
- `adDiscountButton` → AdDiscountButton (Button 컴포넌트)

---

## 2. QuestTab (미션 탭)

### GDD 참조
**섹션 6-4**: 미션 탭 와이어프레임

### GameObject 구조

```
QuestTab (Root)
├── TitleArea (Empty GameObject)
│   ├── TitleText (TextMeshProUGUI)
│   │   └── Text: "📋 일일 미션"
│   │   └── Font Size: 60, Bold, Center
│   │   └── Size: (600, 80)
│   └── QuestCountText (TextMeshProUGUI)
│       └── Text: "일일 미션 (0/7 완료)"
│       └── Font Size: 40, Center
│       └── Size: (600, 60)
│
├── QuestListContainer (Empty GameObject)
│   ├── VerticalLayoutGroup
│   ├── Spacing: 20
│   └── [여기에 QuestItem들이 동적으로 추가됨]
│
├── MilestonePanel (Empty GameObject)
│   ├── Background (Image)
│   ├── TitleText (TextMeshProUGUI)
│   │   └── Text: "🎁 마일스톤 보상"
│   └── MilestoneList (Vertical Layout)
│       ├── Milestone3Text (TextMeshProUGUI)
│       │   └── Text: "⬜ 3개 완료: 오프라인 +1h"
│       ├── Milestone5Text (TextMeshProUGUI)
│       │   └── Text: "⬜ 5개 완료: 오프라인 +1h"
│       └── Milestone7Text (TextMeshProUGUI)
│           └── Text: "⬜ 7개 완료: 오프라인 +1h"
│
└── RefreshArea (Empty GameObject)
    ├── RefreshCountText (TextMeshProUGUI)
    │   └── Text: "🔄 미션 재설정 (무료 0/2)"
    │   └── Font Size: 36, Center
    └── ButtonRow (Horizontal Layout)
        ├── RefreshButton (Button)
        │   └── Text: "재설정"
        │   └── Size: (240, 100)
        └── AdRefreshButton (Button)
            └── Text: "광고로 재설정"
            └── Size: (240, 100)
```

### QuestItemPrefab 구조 (별도 생성)

```
QuestItem (Prefab)
├── Background (Image)
│   └── Size: (600, 120)
├── StatusIcon (TextMeshProUGUI)
│   └── Text: "⬜" or "✅"
│   └── Size: (60, 60)
├── QuestText (TextMeshProUGUI)
│   └── Text: "Easy: 광물 10회 채굴"
│   └── Font Size: 36
└── RewardText (TextMeshProUGUI)
    └── Text: "💎 10"
    └── Font Size: 36
    └── Color: Cyan
```

### QuestTabController 연결

Inspector에서 다음 SerializeField 연결:
- `questCountText` → QuestCountText
- `questListContainer` → QuestListContainer (Transform)
- `questItemPrefab` → QuestItem Prefab
- `refreshQuestButton` → RefreshButton
- `refreshCountText` → RefreshCountText
- `milestone3Text` → Milestone3Text
- `milestone5Text` → Milestone5Text
- `milestone7Text` → Milestone7Text

---

## 3. ShopTab (상점 탭)

### GDD 참조
**섹션 6-5**: 상점 탭 와이어프레임

### GameObject 구조

```
ShopTab (Root)
├── TitleText (TextMeshProUGUI)
│   └── Text: "💎 상점"
│   └── Font Size: 60, Bold, Center
│
├── AdSection (Empty GameObject)
│   ├── SectionTitle (TextMeshProUGUI)
│   │   └── Text: "📺 광고 시청"
│   ├── AdCountText (TextMeshProUGUI)
│   │   └── Text: "📺 광고 시청 (오늘 0/3)"
│   └── AdButtonList (Vertical Layout)
│       ├── AdRow1 (Horizontal Layout)
│       │   ├── AdInfo1 (TextMeshProUGUI)
│       │   │   └── Text: "1회: 크리스탈 +10"
│       │   └── WatchAdButton1 (Button)
│       │       └── Text: "시청"
│       ├── AdRow2 (Horizontal Layout)
│       │   ├── AdInfo2 (TextMeshProUGUI)
│       │   │   └── Text: "2회: 크리스탈 +14"
│       │   └── WatchAdButton2 (Button)
│       │       └── Text: "시청"
│       └── AdRow3 (Horizontal Layout)
│           ├── AdInfo3 (TextMeshProUGUI)
│           │   └── Text: "3회: 크리스탈 +18"
│           └── WatchAdButton3 (Button)
│               └── Text: "시청"
│
├── IAPSection (Empty GameObject) [MVP: UI만]
│   ├── SectionTitle (TextMeshProUGUI)
│   │   └── Text: "💰 크리스탈 패키지 (UI만)"
│   └── IAPButtonList (Vertical Layout)
│       ├── IAPRow1 (Horizontal Layout)
│       │   ├── IAPInfo1 (TextMeshProUGUI)
│       │   │   └── Text: "소량: 100개 - $0.99"
│       │   └── IAPButton1 (Button)
│       │       └── Text: "준비중"
│       │       └── Interactable: False
│       ├── IAPRow2 (Horizontal Layout)
│       │   ├── IAPInfo2 (TextMeshProUGUI)
│       │   │   └── Text: "중량: 500개 - $4.99"
│       │   └── IAPButton2 (Button)
│       │       └── Text: "준비중"
│       │       └── Interactable: False
│       └── IAPRow3 (Horizontal Layout)
│           ├── IAPInfo3 (TextMeshProUGUI)
│           │   └── Text: "대량: 1200개 - $9.99"
│           └── IAPButton3 (Button)
│               └── Text: "준비중"
│               └── Interactable: False
│
└── SlotUnlockSection (Empty GameObject)
    ├── SectionTitle (TextMeshProUGUI)
    │   └── Text: "🎁 슬롯 해금"
    └── SlotButtonList (Vertical Layout)
        ├── SlotRow2 (Horizontal Layout)
        │   ├── Slot2CostText (TextMeshProUGUI)
        │   │   └── Text: "슬롯 2: 400 💎"
        │   └── UnlockSlot2Button (Button)
        │       └── Text: "해금"
        ├── SlotRow3 (Horizontal Layout)
        │   ├── Slot3CostText (TextMeshProUGUI)
        │   │   └── Text: "슬롯 3: 2,000 💎"
        │   └── UnlockSlot3Button (Button)
        │       └── Text: "🔒"
        │       └── Interactable: False
        └── SlotRow4 (Horizontal Layout)
            ├── Slot4CostText (TextMeshProUGUI)
            │   └── Text: "슬롯 4: 4,000 💎"
            └── UnlockSlot4Button (Button)
                └── Text: "🔒"
                └── Interactable: False
```

### ShopTabController 연결

Inspector에서 다음 SerializeField 연결:
- `watchAdButton1/2/3` → WatchAdButton1/2/3
- `adCountText` → AdCountText
- `unlockSlot2/3/4Button` → UnlockSlot2/3/4Button
- `slot2/3/4CostText` → Slot2/3/4CostText
- `iapSmallButton` → IAPButton1
- `iapMediumButton` → IAPButton2
- `iapLargeButton` → IAPButton3

---

## 4. SettingsTab (설정 탭)

### GDD 참조
**섹션 6-6**: 설정 탭 와이어프레임

### GameObject 구조

```
SettingsTab (Root)
├── TitleText (TextMeshProUGUI)
│   └── Text: "⚙️ 설정"
│   └── Font Size: 60, Bold, Center
│
├── SoundSection (Empty GameObject)
│   ├── SectionTitle (TextMeshProUGUI)
│   │   └── Text: "🔊 사운드"
│   ├── BGMRow (Horizontal Layout)
│   │   ├── BGMLabel (TextMeshProUGUI)
│   │   │   └── Text: "BGM:"
│   │   ├── BGMSlider (Slider)
│   │   │   └── Value: 0.8, Min: 0, Max: 1
│   │   │   └── Size: (400, 60)
│   │   └── BGMVolumeText (TextMeshProUGUI)
│   │       └── Text: "80%"
│   │       └── Size: (100, 60)
│   └── SFXRow (Horizontal Layout)
│       ├── SFXLabel (TextMeshProUGUI)
│       │   └── Text: "효과음:"
│       ├── SFXSlider (Slider)
│       │   └── Value: 1.0, Min: 0, Max: 1
│       │   └── Size: (400, 60)
│       └── SFXVolumeText (TextMeshProUGUI)
│           └── Text: "100%"
│           └── Size: (100, 60)
│
├── NotificationSection (Empty GameObject)
│   ├── SectionTitle (TextMeshProUGUI)
│   │   └── Text: "🔔 알림"
│   ├── OfflineNotificationRow (Horizontal Layout)
│   │   ├── OfflineLabel (TextMeshProUGUI)
│   │   │   └── Text: "오프라인 채굴 완료:"
│   │   └── OfflineNotificationToggle (Toggle)
│   │       └── IsOn: True
│   │       └── Size: (80, 60)
│   └── MissionNotificationRow (Horizontal Layout)
│       ├── MissionLabel (TextMeshProUGUI)
│       │   └── Text: "일일 미션 리셋:"
│       └── MissionNotificationToggle (Toggle)
│           └── IsOn: True
│           └── Size: (80, 60)
│
├── AccountSection (Empty GameObject)
│   ├── SectionTitle (TextMeshProUGUI)
│   │   └── Text: "👤 계정"
│   ├── AccountInfoText (TextMeshProUGUI)
│   │   └── Text: "Google Play 연동: ✅"
│   │   └── Font Size: 36
│   └── LogoutButton (Button)
│       └── Text: "로그아웃"
│       └── Size: (400, 100)
│       └── Color: Red (0.8, 0.2, 0.2)
│
└── InfoSection (Empty GameObject)
    ├── SectionTitle (TextMeshProUGUI)
    │   └── Text: "ℹ️ 정보"
    ├── VersionText (TextMeshProUGUI)
    │   └── Text: "버전: 1.0.0 (MVP)"
    │   └── Font Size: 32
    └── LinkButtonRow (Horizontal Layout)
        ├── TermsButton (Button)
        │   └── Text: "이용약관"
        │   └── Size: (180, 80)
        ├── PrivacyButton (Button)
        │   └── Text: "개인정보처리방침"
        │   └── Size: (180, 80)
        └── SupportButton (Button)
            └── Text: "고객지원"
            └── Size: (180, 80)
```

### SettingsTabController 연결

Inspector에서 다음 SerializeField 연결:
- `bgmSlider` → BGMSlider
- `sfxSlider` → SFXSlider
- `bgmVolumeText` → BGMVolumeText
- `sfxVolumeText` → SFXVolumeText
- `offlineNotificationToggle` → OfflineNotificationToggle
- `missionNotificationToggle` → MissionNotificationToggle
- `accountInfoText` → AccountInfoText
- `logoutButton` → LogoutButton
- `versionText` → VersionText
- `termsButton` → TermsButton
- `privacyButton` → PrivacyButton
- `supportButton` → SupportButton

---

## Unity Editor 작업 순서

### 1단계: 탭 Root GameObject 생성
1. Hierarchy에서 Panel GameObject 선택
2. 우클릭 → Create Empty
3. 이름을 `UpgradeTab` (또는 QuestTab, ShopTab, SettingsTab)로 변경
4. RectTransform 설정 적용 (위 공통 구조 참조)

### 2단계: 컴포넌트 추가
1. Add Component → Vertical Layout Group
2. VerticalLayoutGroup 설정 적용 (위 공통 구조 참조)
3. Add Component → [TabName]Controller

### 3단계: 자식 UI 요소 생성
1. 위 GameObject 구조를 참고하여 하나씩 생성
2. 우클릭 → UI → TextMeshPro - Text (또는 Button, Slider, Toggle 등)
3. 각 요소의 RectTransform, Text, Font Size 등 설정

### 4단계: Controller 참조 연결
1. Root GameObject 선택
2. Inspector에서 [TabName]Controller 컴포넌트 찾기
3. 위 "Controller 연결" 섹션 참조하여 SerializeField 드래그 앤 드롭

### 5단계: 초기 비활성화
1. MiningTab 외 모든 탭은 체크박스 해제하여 비활성화
2. GameTabManager의 참조 연결 (GAME_SETUP_GUIDE.md 참조)

---

## 참고사항

### 색상 참조
- **Green** (강화 성공): RGB(0.2, 0.8, 0.2)
- **Gold** (골드): RGB(1, 0.8, 0.2)
- **Cyan** (크리스탈): RGB(0.2, 0.8, 1)
- **Purple** (광고): RGB(0.8, 0.2, 0.8)
- **Red** (로그아웃): RGB(0.8, 0.2, 0.2)
- **White**: RGB(1, 1, 1)
- **Black**: RGB(0, 0, 0)

### Font 설정
- **Title**: 60pt, Bold
- **Section Title**: 48pt, Bold
- **Normal Text**: 36-40pt
- **Small Text**: 32pt

### 버튼 크기 가이드
- **Primary Button**: (500, 120)
- **Secondary Button**: (400, 100)
- **Small Button**: (240, 80)
- **Wide Button**: (600, 100)

---

**작성일**: 2025-12-12
**작성자**: Claude Code Assistant
**GDD 참조**: MVP-Phase1/GDD/GDD_InfinitePickaxe.md, 섹션 6
