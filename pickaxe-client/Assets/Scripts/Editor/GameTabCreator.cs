using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace InfinitePickaxe.Client.Editor
{
    /// <summary>
    /// Game 씬의 4개 탭(Upgrade, Quest, Shop, Settings)을 자동으로 생성하는 에디터 스크립트
    /// 사용법: Unity Editor 메뉴 → Tools → Create Game Tabs
    /// </summary>
    public class GameTabCreator : EditorWindow
    {
        private const string FONT_PATH = "Assets/TextMesh Pro/Resources/Fonts & Materials/NeoDunggeunmoPro-Regular SDF.asset";

        [MenuItem("Tools/Create Game Tabs")]
        public static void ShowWindow()
        {
            GetWindow<GameTabCreator>("Game Tab Creator");
        }

        private void OnGUI()
        {
            GUILayout.Label("Game 씬 탭 자동 생성", EditorStyles.boldLabel);
            GUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "이 도구는 Game 씬의 Panel 아래에 5개 탭을 자동으로 생성합니다.\n" +
                "- MiningTab (채굴 탭) - HP 슬라이더 포함\n" +
                "- UpgradeTab (강화 탭)\n" +
                "- QuestTab (미션 탭)\n" +
                "- ShopTab (상점 탭)\n" +
                "- SettingsTab (설정 탭)\n\n" +
                "실행 전 Scene을 저장하고 백업하는 것을 권장합니다.",
                MessageType.Info
            );

            GUILayout.Space(10);

            if (GUILayout.Button("Create MiningTab", GUILayout.Height(40)))
            {
                CreateMiningTab();
            }

            if (GUILayout.Button("Create UpgradeTab", GUILayout.Height(40)))
            {
                CreateUpgradeTab();
            }

            if (GUILayout.Button("Create QuestTab", GUILayout.Height(40)))
            {
                CreateQuestTab();
            }

            if (GUILayout.Button("Create ShopTab", GUILayout.Height(40)))
            {
                CreateShopTab();
            }

            if (GUILayout.Button("Create SettingsTab", GUILayout.Height(40)))
            {
                CreateSettingsTab();
            }

            GUILayout.Space(20);

            if (GUILayout.Button("Create All Tabs", GUILayout.Height(60)))
            {
                CreateAllTabs();
            }
        }

        private static void CreateAllTabs()
        {
            CreateMiningTab();
            CreateUpgradeTab();
            CreateQuestTab();
            CreateShopTab();
            CreateSettingsTab();

            Debug.Log("모든 탭 생성 완료!");
        }

        #region MiningTab Creation

        private static void CreateMiningTab()
        {
            var panel = GameObject.Find("Panel");
            if (panel == null)
            {
                Debug.LogError("Panel GameObject를 찾을 수 없습니다!");
                return;
            }

            var existing = panel.transform.Find("MiningTab");
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("확인", "기존 MiningTab을 삭제하고 다시 생성하시겠습니까?", "예", "아니오"))
                    return;
                DestroyImmediate(existing.gameObject);
            }

            // Root GameObject 생성 (스크롤 활성화)
            var miningTab = CreateTabRoot("MiningTab", panel.transform, enableScroll: true);

            var layoutGroup = miningTab.AddComponent<VerticalLayoutGroup>();
            layoutGroup.padding = new RectOffset(0, 0, 0, 0);
            layoutGroup.spacing = 20;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;

            miningTab.AddComponent<UI.Game.MiningTabController>();

            // 1. SlotsRow (곡괭이 슬롯 4개)
            var slotsRow = CreateEmpty(miningTab.transform, "SlotsRow", new Vector2(1080, 360));
            var slotsLayout = slotsRow.AddComponent<HorizontalLayoutGroup>();
            slotsLayout.spacing = 20;
            slotsLayout.padding = new RectOffset(40, 40, 40, 40);
            slotsLayout.childAlignment = TextAnchor.MiddleCenter;
            slotsLayout.childControlWidth = false;
            slotsLayout.childControlHeight = false;

            // 배경
            var slotsImage = slotsRow.AddComponent<Image>();
            slotsImage.color = new Color(0, 0, 0, 0.6f);

            // 슬롯 1-4 생성
            for (int i = 1; i <= 4; i++)
            {
                var slot = CreateEmpty(slotsRow.transform, $"Slot{i}", new Vector2(240, 280));
                var slotLayout = slot.AddComponent<VerticalLayoutGroup>();
                slotLayout.spacing = 10;
                slotLayout.padding = new RectOffset(20, 20, 20, 20);
                slotLayout.childAlignment = TextAnchor.MiddleCenter;

                var slotBg = slot.AddComponent<Image>();
                slotBg.color = i == 1 ? new Color(0.2f, 0.2f, 0.2f, 0.8f) : new Color(0.1f, 0.1f, 0.1f, 0.5f);

                // 곡괭이 스프라이트 영역
                var pickaxeArea = CreateEmpty(slot.transform, "PickaxeArea", new Vector2(200, 180));
                var pickaxeImage = pickaxeArea.AddComponent<Image>();
                pickaxeImage.color = i > 1 ? new Color(0.3f, 0.3f, 0.3f) : Color.white;

                // 레벨 텍스트
                CreateText(slot.transform, "LevelText", i == 1 ? "Lv 0" : "잠김", 36, TextAlignmentOptions.Center, new Vector2(200, 50));
            }

            // 2. CenterPanel (채굴 중앙 영역)
            var centerPanel = CreateEmpty(miningTab.transform, "CenterPanel", new Vector2(1080, 1272));
            var centerLayout = centerPanel.AddComponent<VerticalLayoutGroup>();
            centerLayout.spacing = 30;
            centerLayout.padding = new RectOffset(60, 60, 40, 40);
            centerLayout.childAlignment = TextAnchor.UpperCenter;
            centerLayout.childControlWidth = false;
            centerLayout.childControlHeight = false;

            var centerBg = centerPanel.AddComponent<Image>();
            centerBg.color = new Color(0.12f, 0.12f, 0.12f, 0.7f);

            // 광물 정보 텍스트
            var mineInfoText = CreateText(centerPanel.transform, "MineInfoText", "채굴 중: 약한 돌", 48, TextAlignmentOptions.Center, new Vector2(960, 60));
            mineInfoText.GetComponent<TextMeshProUGUI>().color = new Color(1f, 1f, 0.5f);

            // 광물 스프라이트 영역
            var mineralArea = CreateEmpty(centerPanel.transform, "MineralArea", new Vector2(500, 500));
            var mineralImage = mineralArea.AddComponent<Image>();
            mineralImage.color = new Color(0.7f, 0.5f, 0.3f);

            // HP 슬라이더 영역
            var hpSliderContainer = CreateEmpty(centerPanel.transform, "HPSliderContainer", new Vector2(800, 100));
            var hpContainerLayout = hpSliderContainer.AddComponent<VerticalLayoutGroup>();
            hpContainerLayout.spacing = 10;
            hpContainerLayout.childAlignment = TextAnchor.UpperCenter;

            // HP 텍스트
            var mineHPText = CreateText(hpSliderContainer.transform, "MineHPText", "HP: 25/25 (100.0%)", 40, TextAlignmentOptions.Center, new Vector2(800, 50));
            mineHPText.GetComponent<TextMeshProUGUI>().color = new Color(0.2f, 1f, 0.2f);

            // HP 슬라이더
            var sliderGo = new GameObject("MineHPSlider");
            sliderGo.transform.SetParent(hpSliderContainer.transform, false);
            var sliderRect = sliderGo.AddComponent<RectTransform>();
            sliderRect.sizeDelta = new Vector2(800, 50);

            // Slider Background
            var sliderBg = sliderGo.AddComponent<Image>();
            sliderBg.color = new Color(0.2f, 0.2f, 0.2f);

            var slider = sliderGo.AddComponent<Slider>();
            slider.minValue = 0;
            slider.maxValue = 100;
            slider.value = 100;
            slider.interactable = false;

            // Fill Area
            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderGo.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.sizeDelta = new Vector2(-20, -20);

            // Fill
            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;

            var fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.2f, 1f, 0.2f);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;

            slider.fillRect = fillRect;

            // DPS 텍스트
            var dpsText = CreateText(centerPanel.transform, "DPSText", "DPS: 10.0", 52, TextAlignmentOptions.Center, new Vector2(960, 70), FontStyles.Bold);
            dpsText.GetComponent<TextMeshProUGUI>().color = new Color(1f, 0.8f, 0.2f);

            // 광물 선택 버튼
            CreateButton(centerPanel.transform, "SelectMineralButton", "광물 선택", 48, new Vector2(600, 120), new Color(0.2f, 0.6f, 0.8f));

            // 초기 활성화 (MiningTab은 기본 활성 상태)
            miningTab.transform.parent.gameObject.SetActive(true);

            Debug.Log("MiningTab 생성 완료!");
        }

        #endregion

        #region UpgradeTab Creation

        private static void CreateUpgradeTab()
        {
            var panel = GameObject.Find("Panel");
            if (panel == null)
            {
                Debug.LogError("Panel GameObject를 찾을 수 없습니다!");
                return;
            }

            // 기존 UpgradeTab 확인
            var existing = panel.transform.Find("UpgradeTab");
            if (existing != null)
            {
                Debug.LogWarning("UpgradeTab이 이미 존재합니다. 삭제 후 다시 생성하시겠습니까?");
                if (!EditorUtility.DisplayDialog("확인", "기존 UpgradeTab을 삭제하고 다시 생성하시겠습니까?", "예", "아니오"))
                    return;
                DestroyImmediate(existing.gameObject);
            }

            // Root GameObject 생성 (스크롤 활성화)
            var upgradeTab = CreateTabRoot("UpgradeTab", panel.transform, enableScroll: true);

            // VerticalLayoutGroup 설정
            var layoutGroup = upgradeTab.AddComponent<VerticalLayoutGroup>();
            layoutGroup.padding = new RectOffset(40, 40, 40, 40);
            layoutGroup.spacing = 30;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;

            // Controller 추가
            upgradeTab.AddComponent<UI.Game.UpgradeTabController>();

            // 1. Title
            CreateText(upgradeTab.transform, "TitleText", "곡괭이 강화", 60, TextAlignmentOptions.Center, new Vector2(600, 80), FontStyles.Bold);

            // 2. 슬롯 선택 영역
            var slotSelection = CreateEmpty(upgradeTab.transform, "SlotSelection", new Vector2(700, 150));
            var slotLayout = slotSelection.AddComponent<HorizontalLayoutGroup>();
            slotLayout.spacing = 20;
            slotLayout.childAlignment = TextAnchor.MiddleCenter;
            slotLayout.childControlWidth = false;
            slotLayout.childControlHeight = false;

            for (int i = 1; i <= 4; i++)
            {
                var slot = CreateButton(slotSelection.transform, $"Slot{i}Button", $"슬롯 {i}", 36, new Vector2(150, 120));
                // 슬롯 2-4는 초기 비활성화
                if (i > 1)
                {
                    slot.GetComponent<Button>().interactable = false;
                }
            }

            // 3. 곡괭이 정보 영역
            CreateText(upgradeTab.transform, "PickaxeLevelText", "곡괭이 레벨: 0", 48, TextAlignmentOptions.Center, new Vector2(500, 60));
            CreateText(upgradeTab.transform, "CurrentDPSText", "현재 DPS: 10", 40, TextAlignmentOptions.Center, new Vector2(500, 50));
            var nextDPS = CreateText(upgradeTab.transform, "NextDPSText", "다음 DPS: 17 (+70%)", 40, TextAlignmentOptions.Center, new Vector2(500, 50));
            nextDPS.GetComponent<TextMeshProUGUI>().color = new Color(0.2f, 1f, 0.2f);

            // 4. 강화 확률 표시
            var upgradeChance = CreateText(upgradeTab.transform, "UpgradeChanceText", "강화 확률: 100%", 40, TextAlignmentOptions.Center, new Vector2(500, 50));
            upgradeChance.GetComponent<TextMeshProUGUI>().color = new Color(0.2f, 1f, 0.2f);

            // 5. 비용 표시
            var cost = CreateText(upgradeTab.transform, "UpgradeCostText", "강화 비용: 5 골드", 40, TextAlignmentOptions.Center, new Vector2(500, 50));
            cost.GetComponent<TextMeshProUGUI>().color = new Color(1f, 0.8f, 0.2f);

            // 6. 강화 버튼
            CreateButton(upgradeTab.transform, "UpgradeButton", "강화하기", 48, new Vector2(500, 120), new Color(0.2f, 0.8f, 0.2f));

            // 7. 광고 할인 버튼
            CreateButton(upgradeTab.transform, "AdDiscountButton", "광고 보고 -25% (0/3)", 36, new Vector2(500, 100), new Color(0.8f, 0.2f, 0.8f));

            // 초기 비활성화
            upgradeTab.transform.parent.gameObject.SetActive(false);

            Debug.Log("UpgradeTab 생성 완료!");
        }

        #endregion

        #region QuestTab Creation

        private static void CreateQuestTab()
        {
            var panel = GameObject.Find("Panel");
            if (panel == null)
            {
                Debug.LogError("Panel GameObject를 찾을 수 없습니다!");
                return;
            }

            var existing = panel.transform.Find("QuestTab");
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("확인", "기존 QuestTab을 삭제하고 다시 생성하시겠습니까?", "예", "아니오"))
                    return;
                DestroyImmediate(existing.gameObject);
            }

            var questTab = CreateTabRoot("QuestTab", panel.transform, enableScroll: true);

            var layoutGroup = questTab.AddComponent<VerticalLayoutGroup>();
            layoutGroup.padding = new RectOffset(40, 40, 40, 40);
            layoutGroup.spacing = 30;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;

            questTab.AddComponent<UI.Game.QuestTabController>();

            // 1. Title Area
            var titleArea = CreateEmpty(questTab.transform, "TitleArea", new Vector2(600, 140));
            var titleLayout = titleArea.AddComponent<VerticalLayoutGroup>();
            titleLayout.spacing = 10;
            titleLayout.childAlignment = TextAnchor.UpperCenter;

            CreateText(titleArea.transform, "TitleText", "일일 미션", 60, TextAlignmentOptions.Center, new Vector2(600, 80), FontStyles.Bold);
            CreateText(titleArea.transform, "QuestCountText", "0/7 완료", 40, TextAlignmentOptions.Center, new Vector2(600, 60));

            // 2. Quest List Container
            var listContainer = CreateEmpty(questTab.transform, "QuestListContainer", new Vector2(700, 600));
            var listLayout = listContainer.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 20;
            listLayout.childAlignment = TextAnchor.UpperCenter;
            listLayout.childControlWidth = false;
            listLayout.childControlHeight = false;

            // 완료 메시지 (초기 비활성화)
            var completeMsg = CreateText(listContainer.transform, "AllCompleteMessage", "오늘의 모든 미션이 완료되었습니다!", 40, TextAlignmentOptions.Center, new Vector2(700, 100));
            completeMsg.GetComponent<TextMeshProUGUI>().color = new Color(0.5f, 0.5f, 0.5f);
            completeMsg.SetActive(false);

            // 3. Milestone Panel
            var milestonePanel = CreateEmpty(questTab.transform, "MilestonePanel", new Vector2(700, 300));
            var milestoneLayout = milestonePanel.AddComponent<VerticalLayoutGroup>();
            milestoneLayout.spacing = 15;
            milestoneLayout.padding = new RectOffset(20, 20, 20, 20);
            milestoneLayout.childAlignment = TextAnchor.UpperCenter;
            milestoneLayout.childControlWidth = false;
            milestoneLayout.childControlHeight = false;

            // 배경
            var bg = milestonePanel.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);

            CreateText(milestonePanel.transform, "MilestoneTitleText", "마일스톤 보상", 48, TextAlignmentOptions.Center, new Vector2(660, 60), FontStyles.Bold);

            // 마일스톤 3/5/7
            for (int i = 3; i <= 7; i += 2)
            {
                var row = CreateEmpty(milestonePanel.transform, $"Milestone{i}Row", new Vector2(660, 80));
                var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
                rowLayout.spacing = 20;
                rowLayout.childAlignment = TextAnchor.MiddleLeft;
                rowLayout.childControlWidth = false;
                rowLayout.childControlHeight = false;

                CreateText(row.transform, $"Milestone{i}Text", $"{i}개 완료: 오프라인 +1h", 36, TextAlignmentOptions.Left, new Vector2(450, 60));
                CreateButton(row.transform, $"Milestone{i}Button", "수령", 32, new Vector2(150, 60), new Color(0.2f, 0.8f, 0.2f)).GetComponent<Button>().interactable = false;
            }

            // 4. Refresh Area
            var refreshArea = CreateEmpty(questTab.transform, "RefreshArea", new Vector2(700, 150));
            var refreshLayout = refreshArea.AddComponent<VerticalLayoutGroup>();
            refreshLayout.spacing = 15;
            refreshLayout.childAlignment = TextAnchor.UpperCenter;

            CreateText(refreshArea.transform, "RefreshCountText", "미션 재설정 (무료 0/2)", 36, TextAlignmentOptions.Center, new Vector2(700, 50));

            var buttonRow = CreateEmpty(refreshArea.transform, "ButtonRow", new Vector2(500, 80));
            var buttonLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 20;
            buttonLayout.childAlignment = TextAnchor.MiddleCenter;

            CreateButton(buttonRow.transform, "RefreshButton", "재설정", 36, new Vector2(240, 80));
            CreateButton(buttonRow.transform, "AdRefreshButton", "광고로 재설정", 32, new Vector2(240, 80), new Color(0.8f, 0.2f, 0.8f));

            questTab.transform.parent.gameObject.SetActive(false);

            Debug.Log("QuestTab 생성 완료!");
        }

        #endregion

        #region ShopTab Creation

        private static void CreateShopTab()
        {
            var panel = GameObject.Find("Panel");
            if (panel == null)
            {
                Debug.LogError("Panel GameObject를 찾을 수 없습니다!");
                return;
            }

            var existing = panel.transform.Find("ShopTab");
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("확인", "기존 ShopTab을 삭제하고 다시 생성하시겠습니까?", "예", "아니오"))
                    return;
                DestroyImmediate(existing.gameObject);
            }

            var shopTab = CreateTabRoot("ShopTab", panel.transform, enableScroll: true);

            var layoutGroup = shopTab.AddComponent<VerticalLayoutGroup>();
            layoutGroup.padding = new RectOffset(40, 40, 40, 40);
            layoutGroup.spacing = 30;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;

            shopTab.AddComponent<UI.Game.ShopTabController>();

            // 1. Title
            CreateText(shopTab.transform, "TitleText", "상점", 60, TextAlignmentOptions.Center, new Vector2(600, 80), FontStyles.Bold);

            // 2. Ad Section
            var adSection = CreateSection(shopTab.transform, "AdSection", "광고 시청");
            CreateText(adSection.transform, "AdCountText", "광고 시청 (오늘 0/3)", 36, TextAlignmentOptions.Center, new Vector2(700, 50));

            for (int i = 1; i <= 3; i++)
            {
                var row = CreateRowWithButton(adSection.transform, $"AdRow{i}",
                    $"{i}회: 크리스탈 +{10 + (i-1)*4}", "시청", new Vector2(150, 70));
            }

            // 3. IAP Section (STUB)
            var iapSection = CreateSection(shopTab.transform, "IAPSection", "크리스탈 패키지 (준비중)");

            var iapData = new[] {
                ("소량: 100개 - $0.99", "IAPSmallButton"),
                ("중량: 500개 - $4.99", "IAPMediumButton"),
                ("대량: 1200개 - $9.99", "IAPLargeButton")
            };

            foreach (var (text, name) in iapData)
            {
                var row = CreateRowWithButton(iapSection.transform, $"{name}Row", text, "준비중", new Vector2(150, 70));
                row.transform.GetChild(1).GetComponent<Button>().interactable = false;
            }

            // 4. Slot Unlock Section
            var slotSection = CreateSection(shopTab.transform, "SlotUnlockSection", "슬롯 해금");

            var slotData = new[] {
                (2, 400, "UnlockSlot2Button"),
                (3, 2000, "UnlockSlot3Button"),
                (4, 4000, "UnlockSlot4Button")
            };

            foreach (var (slot, cost, name) in slotData)
            {
                var row = CreateRowWithButton(slotSection.transform, $"Slot{slot}Row",
                    $"슬롯 {slot}: {cost:N0} 크리스탈", "해금", new Vector2(150, 70));

                var costText = CreateText(row.transform.GetChild(0).transform, $"Slot{slot}CostText", "", 0, TextAlignmentOptions.Left, Vector2.zero);
                costText.SetActive(false); // Controller가 관리
            }

            shopTab.transform.parent.gameObject.SetActive(false);

            Debug.Log("ShopTab 생성 완료!");
        }

        #endregion

        #region SettingsTab Creation

        private static void CreateSettingsTab()
        {
            var panel = GameObject.Find("Panel");
            if (panel == null)
            {
                Debug.LogError("Panel GameObject를 찾을 수 없습니다!");
                return;
            }

            var existing = panel.transform.Find("SettingsTab");
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("확인", "기존 SettingsTab을 삭제하고 다시 생성하시겠습니까?", "예", "아니오"))
                    return;
                DestroyImmediate(existing.gameObject);
            }

            var settingsTab = CreateTabRoot("SettingsTab", panel.transform, enableScroll: true);

            var layoutGroup = settingsTab.AddComponent<VerticalLayoutGroup>();
            layoutGroup.padding = new RectOffset(40, 40, 40, 40);
            layoutGroup.spacing = 30;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;

            settingsTab.AddComponent<UI.Game.SettingsTabController>();

            // 1. Title
            CreateText(settingsTab.transform, "TitleText", "설정", 60, TextAlignmentOptions.Center, new Vector2(600, 80), FontStyles.Bold);

            // 2. Sound Section
            var soundSection = CreateSection(settingsTab.transform, "SoundSection", "사운드");
            CreateSliderRow(soundSection.transform, "BGM", "BGMSlider", 0.8f);
            CreateSliderRow(soundSection.transform, "SFX", "SFXSlider", 1.0f);

            // 3. Notification Section
            var notiSection = CreateSection(settingsTab.transform, "NotificationSection", "알림");
            CreateToggleRow(notiSection.transform, "OfflineNotificationRow", "오프라인 채굴 완료:", "OfflineNotificationToggle", true);
            CreateToggleRow(notiSection.transform, "MissionNotificationRow", "일일 미션 리셋:", "MissionNotificationToggle", true);

            // 4. Account Section
            var accountSection = CreateSection(settingsTab.transform, "AccountSection", "계정");
            CreateText(accountSection.transform, "AccountInfoText", "Google Play 연동됨", 36, TextAlignmentOptions.Center, new Vector2(700, 60));
            CreateButton(accountSection.transform, "LogoutButton", "로그아웃", 40, new Vector2(400, 100), new Color(0.8f, 0.2f, 0.2f));

            // 5. Info Section
            var infoSection = CreateSection(settingsTab.transform, "InfoSection", "정보");
            CreateText(infoSection.transform, "VersionText", "버전: 1.0.0 (MVP)", 32, TextAlignmentOptions.Center, new Vector2(700, 50));

            var linkRow = CreateEmpty(infoSection.transform, "LinkButtonRow", new Vector2(600, 80));
            var linkLayout = linkRow.AddComponent<HorizontalLayoutGroup>();
            linkLayout.spacing = 15;
            linkLayout.childAlignment = TextAnchor.MiddleCenter;

            CreateButton(linkRow.transform, "TermsButton", "이용약관", 28, new Vector2(180, 80));
            CreateButton(linkRow.transform, "PrivacyButton", "개인정보처리방침", 24, new Vector2(180, 80));
            CreateButton(linkRow.transform, "SupportButton", "고객지원", 28, new Vector2(180, 80));

            settingsTab.transform.parent.gameObject.SetActive(false);

            Debug.Log("SettingsTab 생성 완료!");
        }

        #endregion

        #region Helper Methods

        private static GameObject CreateTabRoot(string name, Transform parent, bool enableScroll = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0.5f);
            rect.anchorMax = new Vector2(1, 0.5f);
            rect.anchoredPosition = new Vector2(0, 48);
            rect.sizeDelta = new Vector2(0, 1632);
            rect.pivot = new Vector2(0.5f, 0.5f);

            // Scroll 기능 추가
            if (enableScroll)
            {
                // ScrollRect 추가
                var scrollRect = go.AddComponent<UnityEngine.UI.ScrollRect>();
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
                scrollRect.scrollSensitivity = 30;

                // Mask 추가
                var mask = go.AddComponent<UnityEngine.UI.Mask>();
                mask.showMaskGraphic = false;

                // Image 추가 (Mask를 위해 필요)
                var image = go.AddComponent<UnityEngine.UI.Image>();
                image.color = new Color(1, 1, 1, 0.01f); // 거의 투명

                // Content GameObject 생성
                var content = new GameObject("Content");
                content.transform.SetParent(go.transform, false);

                var contentRect = content.AddComponent<RectTransform>();
                contentRect.anchorMin = new Vector2(0, 1);
                contentRect.anchorMax = new Vector2(1, 1);
                contentRect.pivot = new Vector2(0.5f, 1);
                contentRect.anchoredPosition = Vector2.zero;
                contentRect.sizeDelta = new Vector2(0, 0); // ContentSizeFitter가 자동 조정

                // ContentSizeFitter 추가 (자식 요소에 맞춰 크기 자동 조정)
                var sizeFitter = content.AddComponent<ContentSizeFitter>();
                sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // ScrollRect에 content 연결
                scrollRect.content = contentRect;

                // VerticalLayoutGroup은 Content에 추가됨
                return content;
            }

            return go;
        }

        private static GameObject CreateEmpty(Transform parent, string name, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = size;

            return go;
        }

        private static GameObject CreateText(Transform parent, string name, string text, float fontSize,
            TextAlignmentOptions alignment, Vector2 size, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = size;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.fontStyle = style;

            // 폰트 설정
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
            if (font != null)
            {
                tmp.font = font;
            }
            else
            {
                Debug.LogWarning($"[주의] 폰트를 찾을 수 없습니다: {FONT_PATH}");
            }

            return go;
        }

        private static GameObject CreateButton(Transform parent, string name, string text, float fontSize,
            Vector2 size, Color? color = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = size;

            var button = go.AddComponent<Button>();
            var image = go.AddComponent<Image>();
            image.color = color ?? new Color(0.2f, 0.8f, 0.2f);
            button.targetGraphic = image;

            // Button Text
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);

            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color.HasValue && color.Value.r > 0.5f ? Color.black : Color.white;

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
            if (font != null)
            {
                tmp.font = font;
            }

            return go;
        }

        private static GameObject CreateSection(Transform parent, string name, string title)
        {
            var section = CreateEmpty(parent, name, new Vector2(700, 300));
            var layout = section.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 15;
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            var bg = section.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.2f, 0.3f);

            CreateText(section.transform, "SectionTitle", title, 44, TextAlignmentOptions.Center, new Vector2(660, 60), FontStyles.Bold);

            return section;
        }

        private static GameObject CreateRowWithButton(Transform parent, string name, string labelText,
            string buttonText, Vector2 buttonSize)
        {
            var row = CreateEmpty(parent, name, new Vector2(660, 70));
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            CreateText(row.transform, "Label", labelText, 32, TextAlignmentOptions.Left, new Vector2(450, 60));
            CreateButton(row.transform, "Button", buttonText, 32, buttonSize);

            return row;
        }

        private static void CreateSliderRow(Transform parent, string label, string sliderName, float defaultValue)
        {
            var row = CreateEmpty(parent, $"{label}Row", new Vector2(660, 70));
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 15;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            CreateText(row.transform, $"{label}Label", $"{label}:", 36, TextAlignmentOptions.Left, new Vector2(100, 60));

            var sliderGo = new GameObject(sliderName);
            sliderGo.transform.SetParent(row.transform, false);
            var sliderRect = sliderGo.AddComponent<RectTransform>();
            sliderRect.sizeDelta = new Vector2(350, 60);

            var slider = sliderGo.AddComponent<Slider>();
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = defaultValue;

            CreateText(row.transform, $"{label}VolumeText", $"{(int)(defaultValue * 100)}%", 36, TextAlignmentOptions.Center, new Vector2(100, 60));
        }

        private static void CreateToggleRow(Transform parent, string rowName, string labelText,
            string toggleName, bool defaultValue)
        {
            var row = CreateEmpty(parent, rowName, new Vector2(660, 70));
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            CreateText(row.transform, "Label", labelText, 36, TextAlignmentOptions.Left, new Vector2(500, 60));

            var toggleGo = new GameObject(toggleName);
            toggleGo.transform.SetParent(row.transform, false);
            var toggleRect = toggleGo.AddComponent<RectTransform>();
            toggleRect.sizeDelta = new Vector2(80, 60);

            var toggle = toggleGo.AddComponent<Toggle>();
            toggle.isOn = defaultValue;
        }

        #endregion
    }
}
