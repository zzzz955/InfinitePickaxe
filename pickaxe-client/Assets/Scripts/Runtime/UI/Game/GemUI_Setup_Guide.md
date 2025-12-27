# Gem UI 설정 가이드

## 1. Pickaxe Slot 젬 아이콘 (GemSlotIconsView)

### 프리팹 구조

각 Pickaxe Slot 버튼 하위에 다음 구조를 생성하세요:

```
Slot1 (Button)
└── GemSlotIcons (GameObject)
    ├── GemIcon1 (Image)
    ├── GemIcon2 (Image)
    ├── GemIcon3 (Image)
    ├── GemIcon4 (Image)
    ├── GemIcon5 (Image)
    └── GemIcon6 (Image)
```

### 설정 단계

1. **GemSlotIcons GameObject 생성**
   - Hierarchy: `Slot1` 우클릭 → Create Empty
   - 이름: `GemSlotIcons`
   - `GemSlotIconsView.cs` 컴포넌트 추가

2. **Layout 설정**
   - `GemSlotIcons`에 `HorizontalLayoutGroup` 컴포넌트 추가
   - Spacing: 2
   - Child Alignment: Middle Left
   - Child Force Expand: Width ✗, Height ✗
   - Child Control Size: Width ✓, Height ✓

3. **RectTransform 설정**
   - Anchor: Bottom-Right (곡괭이 이미지 우하단)
   - Position: X=-10, Y=10
   - Size: Auto (HorizontalLayoutGroup이 자동 계산)
   - **중요**: 빈 슬롯과 잠긴 슬롯은 자동으로 숨김 처리됩니다

4. **GemIcon 이미지 생성**
   - `GemSlotIcons` 하위에 Image 6개 생성
   - 이름: `GemIcon1` ~ `GemIcon6`
   - Image Type: Simple
   - Preserve Aspect: ✓
   - Size: Width=20, Height=20 (권장)

5. **인스펙터 연결**
   - MiningTabController 선택
   - `Slot1GemIcons` → `Slot1/GemSlotIcons` 드래그
   - `Slot2GemIcons` → `Slot2/GemSlotIcons` 드래그
   - `Slot3GemIcons` → `Slot3/GemSlotIcons` 드래그
   - `Slot4GemIcons` → `Slot4/GemSlotIcons` 드래그

6. **GemSlotIconsView 설정**
   - 각 `GemSlotIcons` GameObject 선택
   - `GemIcon1~6` 필드에 해당 이미지 드래그
   - `Equipped Color`: 기본값(White) 사용 또는 원하는 색상 지정
   - ~~Empty Slot Sprite~~, ~~Locked Slot Sprite~~ 필드 제거됨 (빈/잠긴 슬롯은 숨김 처리)

---

## 2. PickaxeInfoModal 젬 섹션

### 프리팹 구조

PickaxeInfoModal 하위에 다음 구조를 추가하세요:

```
PickaxeInfoModal
└── ModalPanel
    ├── (기존 UI...)
    └── GemSection (GameObject)
        ├── GemSectionTitle (TextMeshProUGUI)
        ├── GemSlotsContainer (GameObject)
        │   └── GemSlotItemTemplate (GameObject)
        │       ├── Background (Image)
        │       ├── GemIcon (Image)
        │       ├── GemNameText (TextMeshProUGUI)
        │       ├── GemStatsText (TextMeshProUGUI)
        │       ├── LockedOverlay (GameObject)
        │       │   └── LockIcon (Image)
        │       └── EmptyOverlay (GameObject)
        │           └── EmptyText (TextMeshProUGUI)
        └── Separator (Image) - 선택사항
```

### 설정 단계

#### 2.1. GemSection 생성

1. **GemSection GameObject**
   - `ModalPanel` 하위에 Empty GameObject 생성
   - 이름: `GemSection`
   - RectTransform:
     - Anchor: Stretch-Horizontal (좌우 늘림)
     - Position Y: 능력치 텍스트들 아래
     - Height: 200~300 (자동 조정)

2. **Vertical Layout Group 추가** (선택사항)
   - `GemSection`에 `VerticalLayoutGroup` 추가
   - Spacing: 10
   - Child Force Expand: Height ✓

#### 2.2. GemSectionTitle 생성

1. **TextMeshProUGUI 생성**
   - `GemSection` 하위에 TextMeshProUGUI 생성
   - 이름: `GemSectionTitle`
   - Text: "보석 슬롯 (0/6)" (기본값)
   - Font Size: 24
   - Alignment: Center

#### 2.3. GemSlotsContainer 생성

1. **Container GameObject**
   - `GemSection` 하위에 Empty GameObject 생성
   - 이름: `GemSlotsContainer`
   - RectTransform:
     - Anchor: Top-Stretch (상단 고정, 좌우 늘림)
   - `VerticalLayoutGroup` 추가:
     - Spacing: 5
     - Padding: Left=10, Right=10, Top=10, Bottom=10
     - Child Force Expand: Width ✓, Height ✗
     - Child Control Size: Width ✓, Height ✓
   - `ContentSizeFitter` 추가 (선택사항):
     - Vertical Fit: Preferred Size

#### 2.4. GemSlotItemTemplate 생성

1. **Template GameObject**
   - `GemSlotsContainer` 하위에 Empty GameObject 생성
   - 이름: `GemSlotItemTemplate`
   - RectTransform:
     - Anchor: Top-Stretch (상단 고정, 좌우 늘림)
     - Height: 60
     - **중요**: Width는 0이 아닌 자동(Stretch)이어야 함
   - `LayoutElement` 추가 (권장):
     - Min Height: 60
     - Preferred Height: 60
     - Flexible Width: 1

2. **Background Image**
   - Template 하위에 Image 생성
   - 이름: `Background`
   - Anchor: Stretch (전체 늘림)
   - Color: 약간 어두운 색 (0.2, 0.2, 0.2, 0.8)

3. **GemIcon Image**
   - Template 하위에 Image 생성
   - 이름: `GemIcon`
   - Anchor: Left-Center
   - Position: X=10
   - Size: 50x50
   - Preserve Aspect: ✓

4. **GemNameText**
   - Template 하위에 TextMeshProUGUI 생성
   - 이름: `GemNameText`
   - Anchor: Left-Center
   - Position: X=70, Y=10
   - Text: "보석 이름"
   - Font Size: 18

5. **GemStatsText**
   - Template 하위에 TextMeshProUGUI 생성
   - 이름: `GemStatsText`
   - Anchor: Left-Center
   - Position: X=70, Y=-10
   - Text: "공격력 +5%"
   - Font Size: 14
   - Color: 회색

6. **LockedOverlay**
   - Template 하위에 Empty GameObject 생성
   - 이름: `LockedOverlay`
   - RectTransform:
     - Anchor: Stretch (전체 늘림)
     - Offset: (0, 0, 0, 0)
   - Image 컴포넌트 추가:
     - Color: (0, 0, 0, 180) 반투명 검은색
   - 하위 요소:
     - **LockIcon** (Image):
       - Anchor: Middle-Center
       - Size: 40x40
       - Sprite: 자물쇠 아이콘
     - **LockNameText** (TextMeshProUGUI):
       - Anchor: Middle-Center
       - Position: Y=-30
       - Text: "잠김"
       - Font Size: 16
       - Color: White
     - **LockStatsText** (TextMeshProUGUI):
       - Anchor: Middle-Center
       - Position: Y=-50
       - Text: "슬롯 해금 필요"
       - Font Size: 12
       - Color: Gray
   - **기본 비활성화** (Active 체크 해제)

7. **EmptyOverlay**
   - Template 하위에 Empty GameObject 생성
   - 이름: `EmptyOverlay`
   - RectTransform:
     - Anchor: Stretch (전체 늘림)
     - Offset: (0, 0, 0, 0)
   - Image 컴포넌트 추가 (선택사항):
     - Color: (0.2, 0.2, 0.2, 0.5) 반투명 회색
   - 하위 요소:
     - **EmptyText** (TextMeshProUGUI):
       - Anchor: Middle-Center
       - Text: "빈 슬롯"
       - Font Size: 16
       - Color: Gray
       - Alignment: Center
   - **기본 비활성화** (Active 체크 해제)

8. **Template 비활성화**
   - `GemSlotItemTemplate`의 Active 체크 해제
   - (런타임에 복제되어 사용됨)

#### 2.5. 인스펙터 연결

1. **MiningTabController 선택**
2. **Pickaxe Info Modal - Gem Section 섹션**:
   - `Pickaxe Info Gem Section`: `GemSection` 드래그
   - `Gem Section Title Text`: `GemSectionTitle` 드래그
   - `Gem Slots Container`: `GemSlotsContainer` 드래그
   - `Gem Slot Item Template`: `GemSlotItemTemplate` 드래그

---

## 3. 레이아웃 예시

### Pickaxe Slot 젬 아이콘 배치

```
┌─────────────────┐
│  [곡괭이 이미지]  │
│                 │
│        Lv 5     │
│                 │
│      💎💎💎      │ ← 젬 아이콘 (수평 배치, 장착된 것만 표시)
│                 │   (빈/잠긴 슬롯은 숨김)
└─────────────────┘
```

### PickaxeInfoModal 젬 섹션

```
┌─────────────────────────────┐
│ 곡괭이 정보 (Lv 5)          │
├─────────────────────────────┤
│ [곡괭이 이미지]              │
│ 공격력: 1,200               │
│ 공격속도: 1.00              │
│ DPS: 1,200                  │
│ 크리티컬 확률: 5.0%         │
│ 크리티컬 데미지: 150.0%     │
│ ──────────────────          │
│ 보석 슬롯 (2/6)             │ ← GemSectionTitle
│ ┌─────────────────────────┐ │
│ │ [💎] 공격 보석          │ │
│ │     공격력 +5%          │ │
│ ├─────────────────────────┤ │
│ │ [💎] 공속 보석          │ │
│ │     공격속도 +3%        │ │
│ ├─────────────────────────┤ │
│ │ [🔒] 잠김               │ │
│ │     슬롯 해금 필요      │ │
│ └─────────────────────────┘ │
│ [강화하기] [닫기]            │
└─────────────────────────────┘
```

---

## 4. 스프라이트 준비

다음 스프라이트를 준비하세요:

1. **젬 아이콘**: 등급별/타입별 젬 아이콘 (추후 SpriteAtlas 연동)
2. **빈 슬롯**: 빈 슬롯 표시용 (회색 테두리 사각형 등)
3. **잠긴 슬롯**: 자물쇠 아이콘
4. **배경**: GemSlotItemTemplate 배경 (어두운 패널)

---

## 5. 추가 작업 (선택사항)

### 5.1. 애니메이션 추가

- GemSlotItemTemplate에 Animator 추가
- 젬 장착/해제 시 애니메이션 재생

### 5.2. 툴팁 추가

- GemSlotItemView에 Button 또는 EventTrigger 추가
- 클릭 시 젬 상세 정보 모달 표시

### 5.3. 스프라이트 아틀라스 연동

- `SpriteAtlasCache.GetGemSprite(gemId)` 구현
- GemMetaResolver와 연동하여 젬 아이콘 표시

---

## 6. 테스트

1. **Unity Editor 재생**
2. **MiningTab 열기**
3. **Pickaxe Slot 버튼 확인**
   - 젬 아이콘이 표시되는지 확인 (현재는 빈 슬롯)
4. **Pickaxe Slot 클릭**
5. **PickaxeInfoModal 확인**
   - 젬 섹션이 표시되는지 확인
   - 젬 정보가 올바르게 렌더링되는지 확인

---

## 7. 완료 체크리스트

- [ ] GemSlotIconsView 프리팹 생성 (Slot1~4)
- [ ] GemSection 구조 생성
- [ ] GemSlotItemTemplate 생성
- [ ] MiningTabController 인스펙터 연결
- [ ] 스프라이트 할당
- [ ] 테스트 실행
- [ ] GemMetaResolver 연동 (TODO 부분)
- [ ] SpriteAtlasCache 젬 스프라이트 연동 (TODO 부분)

---

## 8. 문제 해결 (Troubleshooting)

### 문제 1: GemSlotItem의 Width가 0으로 표시됨

**증상**: GemSlotItem_0, GemSlotItem_1 등의 Width가 0으로 설정되어 글씨가 겹쳐보이고 이미지가 출력되지 않음

**원인**:
- GemSlotItemTemplate의 RectTransform Anchor가 잘못 설정됨
- LayoutElement가 없어서 VerticalLayoutGroup이 크기를 제대로 계산하지 못함

**해결 방법**:
1. **GemSlotItemTemplate 선택**
2. **RectTransform 설정**:
   - Anchor Preset: Top-Stretch (상단 고정, 좌우 늘림)
   - Left: 0, Right: 0
   - Height: 60
3. **LayoutElement 컴포넌트 추가**:
   - Add Component → Layout → Layout Element
   - Min Height: 60
   - Preferred Height: 60
   - Flexible Width: 1
4. **GemSlotsContainer의 VerticalLayoutGroup 확인**:
   - Child Force Expand: Width ✓, Height ✗
   - Child Control Size: Width ✓, Height ✓

### 문제 2: EmptyOverlay 활성화 시에도 "빈 슬롯" 텍스트가 중복 표시됨

**증상**: EmptyOverlay가 활성화되어 있는데도 GemNameText에 "빈 슬롯"이 노출됨

**원인**:
- 이전 버전의 GemSlotItemView 스크립트가 EmptyOverlay 활성화 시에도 gemNameText를 설정하고 있었음
- EmptyOverlay와 기본 UI 요소가 동시에 표시됨

**해결 방법**:
- **스크립트 업데이트 완료**: `MiningTabController.PickaxeInfoGems.cs` 파일이 자동으로 수정되었습니다
- SetEmpty()와 SetLocked() 메서드에서 Overlay 활성화 시 gemIcon, gemNameText, gemStatsText를 `enabled = false`로 설정
- EmptyOverlay 내부의 EmptyText가 "빈 슬롯"을 표시
- LockedOverlay 내부의 텍스트들이 "잠김", "슬롯 해금 필요"를 표시

### 문제 3: 빈 슬롯이지만 fallback 값이 노출됨

**증상**: PickaxeInfoModal 최초 조회 시 보석 슬롯이 올바르게 렌더링되지 않음

**원인**:
- GemSlotItemView의 AutoBindReferences()가 Awake()에서 호출되지만, 템플릿이 비활성화 상태라서 바인딩이 실패할 수 있음
- 또는 UpdateSlot()이 호출되기 전에 기본값이 표시됨

**해결 방법**:
1. **GemSlotItemTemplate의 기본 상태 확인**:
   - GemIcon, GemNameText, GemStatsText는 기본적으로 비활성화 (enabled = false) 또는 투명하게 설정
   - EmptyOverlay와 LockedOverlay는 기본 비활성화 (Active = false)
2. **템플릿 복제 후 즉시 UpdateSlot() 호출**:
   - 스크립트는 이미 올바르게 구현되어 있음 (EnsureGemSlotItems → UpdateGemSlotItems)
3. **AutoBind 검증**:
   - Unity Editor에서 GemSlotItem_0을 선택하여 GemSlotItemView 컴포넌트의 UI References가 올바르게 연결되었는지 확인

### 문제 4: Visual Settings의 Sprite 할당

**질문**: GemSlotItemView 스크립트의 Visual Settings에 emptySlotSprite, lockedSlotSprite를 할당해야 하나?

**답변**: **할당할 필요 없음**
- **이전 버전**: emptySlotSprite와 lockedSlotSprite를 gemIcon에 설정
- **현재 버전**: Overlay 방식 사용
  - EmptyOverlay 내부의 EmptyText가 "빈 슬롯" 표시
  - LockedOverlay 내부의 LockIcon + 텍스트가 "잠김" 표시
  - gemIcon, gemNameText, gemStatsText는 Overlay 활성화 시 숨김 처리
- **Visual Settings 필드 제거됨**: 스크립트에서 해당 필드들이 삭제되었습니다

---

## 9. TODO 항목 (추후 작업)

다음 파일들의 TODO 주석을 해결해야 합니다:

1. **GemSlotIconsView.cs:108**
   ```csharp
   // TODO: SpriteAtlasCache.GetGemSprite(gemId) 구현 후 연동
   ```

2. **GemSlotItemView.cs:105, 140, 160**
   ```csharp
   // TODO: GemMetaResolver 연동
   ```

3. **GemMetaResolver와 SpriteAtlasCache 통합**
   - 젬 메타데이터에서 아이콘 이름 가져오기
   - 아틀라스에서 스프라이트 로드

---

## 작성자 노트

- 이 가이드는 UI 프리팹과 스크립트만 제공합니다.
- 사용자가 직접 하이어라키에서 구조를 생성하고 인스펙터를 연결해야 합니다.
- 스프라이트 연동은 추후 작업으로 남겨두었습니다.
