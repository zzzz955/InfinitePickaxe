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
            CreateModals();

            Debug.Log("모든 탭 및 모달 생성 완료!");
        }

        private static void CreateModals()
        {
            CreatePickaxeInfoModal();
            CreateLockedSlotModal();
            CreateMineralSelectModal();
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

            // 슬롯 1 생성
            var slot1 = CreateEmpty(slotsRow.transform, "Slot1", new Vector2(240, 280));
            var slot1Layout = slot1.AddComponent<VerticalLayoutGroup>();
            slot1Layout.spacing = 10;
            slot1Layout.padding = new RectOffset(20, 20, 20, 20);
            slot1Layout.childAlignment = TextAnchor.MiddleCenter;

            var slot1Bg = slot1.AddComponent<Image>();
            slot1Bg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            var slot1Button = slot1.AddComponent<Button>();
            slot1Button.targetGraphic = slot1Bg;

            var pickaxeArea1 = CreateEmpty(slot1.transform, "PickaxeArea", new Vector2(200, 180));
            var pickaxeImage1 = pickaxeArea1.AddComponent<Image>();
            pickaxeImage1.color = Color.white;

            CreateText(slot1.transform, "LevelText", "Lv 0", 36, TextAlignmentOptions.Center, new Vector2(200, 50));

            // 슬롯 2 생성
            var slot2 = CreateEmpty(slotsRow.transform, "Slot2", new Vector2(240, 280));
            var slot2Layout = slot2.AddComponent<VerticalLayoutGroup>();
            slot2Layout.spacing = 10;
            slot2Layout.padding = new RectOffset(20, 20, 20, 20);
            slot2Layout.childAlignment = TextAnchor.MiddleCenter;

            var slot2Bg = slot2.AddComponent<Image>();
            slot2Bg.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);

            var slot2Button = slot2.AddComponent<Button>();
            slot2Button.targetGraphic = slot2Bg;

            var pickaxeArea2 = CreateEmpty(slot2.transform, "PickaxeArea", new Vector2(200, 180));
            var pickaxeImage2 = pickaxeArea2.AddComponent<Image>();
            pickaxeImage2.color = new Color(0.3f, 0.3f, 0.3f);

            CreateText(slot2.transform, "LevelText", "잠김", 36, TextAlignmentOptions.Center, new Vector2(200, 50));

            // 슬롯 3 생성
            var slot3 = CreateEmpty(slotsRow.transform, "Slot3", new Vector2(240, 280));
            var slot3Layout = slot3.AddComponent<VerticalLayoutGroup>();
            slot3Layout.spacing = 10;
            slot3Layout.padding = new RectOffset(20, 20, 20, 20);
            slot3Layout.childAlignment = TextAnchor.MiddleCenter;

            var slot3Bg = slot3.AddComponent<Image>();
            slot3Bg.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);

            var slot3Button = slot3.AddComponent<Button>();
            slot3Button.targetGraphic = slot3Bg;

            var pickaxeArea3 = CreateEmpty(slot3.transform, "PickaxeArea", new Vector2(200, 180));
            var pickaxeImage3 = pickaxeArea3.AddComponent<Image>();
            pickaxeImage3.color = new Color(0.3f, 0.3f, 0.3f);

            CreateText(slot3.transform, "LevelText", "잠김", 36, TextAlignmentOptions.Center, new Vector2(200, 50));

            // 슬롯 4 생성
            var slot4 = CreateEmpty(slotsRow.transform, "Slot4", new Vector2(240, 280));
            var slot4Layout = slot4.AddComponent<VerticalLayoutGroup>();
            slot4Layout.spacing = 10;
            slot4Layout.padding = new RectOffset(20, 20, 20, 20);
            slot4Layout.childAlignment = TextAnchor.MiddleCenter;

            var slot4Bg = slot4.AddComponent<Image>();
            slot4Bg.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);

            var slot4Button = slot4.AddComponent<Button>();
            slot4Button.targetGraphic = slot4Bg;

            var pickaxeArea4 = CreateEmpty(slot4.transform, "PickaxeArea", new Vector2(200, 180));
            var pickaxeImage4 = pickaxeArea4.AddComponent<Image>();
            pickaxeImage4.color = new Color(0.3f, 0.3f, 0.3f);

            CreateText(slot4.transform, "LevelText", "잠김", 36, TextAlignmentOptions.Center, new Vector2(200, 50));

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
            var selectMineralButton = CreateButton(centerPanel.transform, "SelectMineralButton", "광물 선택", 48, new Vector2(600, 120), new Color(0.2f, 0.6f, 0.8f));

            // 초기 활성화 (MiningTab은 기본 활성 상태)
            miningTab.transform.parent.gameObject.SetActive(true);

            // Controller SerializeField 자동 매핑
            var controller = miningTab.GetComponent<UI.Game.MiningTabController>();
            if (controller != null)
            {
                var serializedObject = new SerializedObject(controller);

                // mineInfoText 매핑
                var mineInfoTextProp = serializedObject.FindProperty("mineInfoText");
                if (mineInfoTextProp != null)
                    mineInfoTextProp.objectReferenceValue = mineInfoText.GetComponent<TextMeshProUGUI>();

                // mineHPSlider 매핑
                var mineHPSliderProp = serializedObject.FindProperty("mineHPSlider");
                if (mineHPSliderProp != null)
                    mineHPSliderProp.objectReferenceValue = slider;

                // mineHPText 매핑
                var mineHPTextProp = serializedObject.FindProperty("mineHPText");
                if (mineHPTextProp != null)
                    mineHPTextProp.objectReferenceValue = mineHPText.GetComponent<TextMeshProUGUI>();

                // dpsText 매핑
                var dpsTextProp = serializedObject.FindProperty("dpsText");
                if (dpsTextProp != null)
                    dpsTextProp.objectReferenceValue = dpsText.GetComponent<TextMeshProUGUI>();

                // selectMineralButton 매핑
                var selectMineralButtonProp = serializedObject.FindProperty("selectMineralButton");
                if (selectMineralButtonProp != null)
                    selectMineralButtonProp.objectReferenceValue = selectMineralButton.GetComponent<Button>();

                // 슬롯 버튼 매핑
                var pickaxeSlot1ButtonProp = serializedObject.FindProperty("pickaxeSlot1Button");
                if (pickaxeSlot1ButtonProp != null)
                    pickaxeSlot1ButtonProp.objectReferenceValue = slot1Button;

                var pickaxeSlot2ButtonProp = serializedObject.FindProperty("pickaxeSlot2Button");
                if (pickaxeSlot2ButtonProp != null)
                    pickaxeSlot2ButtonProp.objectReferenceValue = slot2Button;

                var pickaxeSlot3ButtonProp = serializedObject.FindProperty("pickaxeSlot3Button");
                if (pickaxeSlot3ButtonProp != null)
                    pickaxeSlot3ButtonProp.objectReferenceValue = slot3Button;

                var pickaxeSlot4ButtonProp = serializedObject.FindProperty("pickaxeSlot4Button");
                if (pickaxeSlot4ButtonProp != null)
                    pickaxeSlot4ButtonProp.objectReferenceValue = slot4Button;

                // 모달 참조 매핑
                var panelForModals = GameObject.Find("Panel");
                if (panelForModals != null)
                {
                    var pickaxeInfoModalObj = panelForModals.transform.Find("PickaxeInfoModal");
                    if (pickaxeInfoModalObj != null)
                    {
                        var pickaxeInfoModalProp = serializedObject.FindProperty("pickaxeInfoModal");
                        if (pickaxeInfoModalProp != null)
                            pickaxeInfoModalProp.objectReferenceValue = pickaxeInfoModalObj.gameObject;
                    }

                    var lockedSlotModalObj = panelForModals.transform.Find("LockedSlotModal");
                    if (lockedSlotModalObj != null)
                    {
                        var lockedSlotModalProp = serializedObject.FindProperty("lockedSlotModal");
                        if (lockedSlotModalProp != null)
                            lockedSlotModalProp.objectReferenceValue = lockedSlotModalObj.gameObject;
                    }

                    var mineralSelectModalObj = panelForModals.transform.Find("MineralSelectModal");
                    if (mineralSelectModalObj != null)
                    {
                        var mineralSelectModalProp = serializedObject.FindProperty("mineralSelectModal");
                        if (mineralSelectModalProp != null)
                            mineralSelectModalProp.objectReferenceValue = mineralSelectModalObj.gameObject;
                    }
                }

                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(controller);

                Debug.Log("MiningTabController SerializeField 자동 매핑 완료 (슬롯 버튼 4개, 모달 3개 포함)!");
            }

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
            var pickaxeLevelText = CreateText(upgradeTab.transform, "PickaxeLevelText", "곡괭이 레벨: 0", 48, TextAlignmentOptions.Center, new Vector2(500, 60));
            var currentDPSText = CreateText(upgradeTab.transform, "CurrentDPSText", "현재 DPS: 10", 40, TextAlignmentOptions.Center, new Vector2(500, 50));
            var nextDPSText = CreateText(upgradeTab.transform, "NextDPSText", "다음 DPS: 17 (+70%)", 40, TextAlignmentOptions.Center, new Vector2(500, 50));
            nextDPSText.GetComponent<TextMeshProUGUI>().color = new Color(0.2f, 1f, 0.2f);

            // 4. 강화 확률 표시
            var upgradeChance = CreateText(upgradeTab.transform, "UpgradeChanceText", "강화 확률: 100%", 40, TextAlignmentOptions.Center, new Vector2(500, 50));
            upgradeChance.GetComponent<TextMeshProUGUI>().color = new Color(0.2f, 1f, 0.2f);

            // 5. 비용 표시
            var upgradeCostText = CreateText(upgradeTab.transform, "UpgradeCostText", "강화 비용: 5 골드", 40, TextAlignmentOptions.Center, new Vector2(500, 50));
            upgradeCostText.GetComponent<TextMeshProUGUI>().color = new Color(1f, 0.8f, 0.2f);

            // 6. 강화 버튼
            var upgradeButton = CreateButton(upgradeTab.transform, "UpgradeButton", "강화하기", 48, new Vector2(500, 120), new Color(0.2f, 0.8f, 0.2f));

            // 7. 광고 할인 버튼
            var adDiscountButton = CreateButton(upgradeTab.transform, "AdDiscountButton", "광고 보고 -25% (0/3)", 36, new Vector2(500, 100), new Color(0.8f, 0.2f, 0.8f));

            // 초기 비활성화
            upgradeTab.transform.parent.gameObject.SetActive(false);

            // Controller SerializeField 자동 매핑
            var controller = upgradeTab.GetComponent<UI.Game.UpgradeTabController>();
            if (controller != null)
            {
                var serializedObject = new SerializedObject(controller);

                var pickaxeLevelTextProp = serializedObject.FindProperty("pickaxeLevelText");
                if (pickaxeLevelTextProp != null)
                    pickaxeLevelTextProp.objectReferenceValue = pickaxeLevelText.GetComponent<TextMeshProUGUI>();

                var currentDPSTextProp = serializedObject.FindProperty("currentDPSText");
                if (currentDPSTextProp != null)
                    currentDPSTextProp.objectReferenceValue = currentDPSText.GetComponent<TextMeshProUGUI>();

                var nextDPSTextProp = serializedObject.FindProperty("nextDPSText");
                if (nextDPSTextProp != null)
                    nextDPSTextProp.objectReferenceValue = nextDPSText.GetComponent<TextMeshProUGUI>();

                var upgradeCostTextProp = serializedObject.FindProperty("upgradeCostText");
                if (upgradeCostTextProp != null)
                    upgradeCostTextProp.objectReferenceValue = upgradeCostText.GetComponent<TextMeshProUGUI>();

                var upgradeButtonProp = serializedObject.FindProperty("upgradeButton");
                if (upgradeButtonProp != null)
                    upgradeButtonProp.objectReferenceValue = upgradeButton.GetComponent<Button>();

                var adDiscountButtonProp = serializedObject.FindProperty("adDiscountButton");
                if (adDiscountButtonProp != null)
                    adDiscountButtonProp.objectReferenceValue = adDiscountButton.GetComponent<Button>();

                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(controller);

                Debug.Log("UpgradeTabController SerializeField 자동 매핑 완료!");
            }

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
            var questCountText = CreateText(titleArea.transform, "QuestCountText", "0/7 완료", 40, TextAlignmentOptions.Center, new Vector2(600, 60));

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

            // 마일스톤 3
            var milestone3Row = CreateEmpty(milestonePanel.transform, "Milestone3Row", new Vector2(660, 80));
            var milestone3Layout = milestone3Row.AddComponent<HorizontalLayoutGroup>();
            milestone3Layout.spacing = 20;
            milestone3Layout.childAlignment = TextAnchor.MiddleLeft;
            milestone3Layout.childControlWidth = false;
            milestone3Layout.childControlHeight = false;

            var milestone3Text = CreateText(milestone3Row.transform, "Milestone3Text", "3개 완료: 오프라인 +1h", 36, TextAlignmentOptions.Left, new Vector2(450, 60));
            CreateButton(milestone3Row.transform, "Milestone3Button", "수령", 32, new Vector2(150, 60), new Color(0.2f, 0.8f, 0.2f)).GetComponent<Button>().interactable = false;

            // 마일스톤 5
            var milestone5Row = CreateEmpty(milestonePanel.transform, "Milestone5Row", new Vector2(660, 80));
            var milestone5Layout = milestone5Row.AddComponent<HorizontalLayoutGroup>();
            milestone5Layout.spacing = 20;
            milestone5Layout.childAlignment = TextAnchor.MiddleLeft;
            milestone5Layout.childControlWidth = false;
            milestone5Layout.childControlHeight = false;

            var milestone5Text = CreateText(milestone5Row.transform, "Milestone5Text", "5개 완료: 오프라인 +1h", 36, TextAlignmentOptions.Left, new Vector2(450, 60));
            CreateButton(milestone5Row.transform, "Milestone5Button", "수령", 32, new Vector2(150, 60), new Color(0.2f, 0.8f, 0.2f)).GetComponent<Button>().interactable = false;

            // 마일스톤 7
            var milestone7Row = CreateEmpty(milestonePanel.transform, "Milestone7Row", new Vector2(660, 80));
            var milestone7Layout = milestone7Row.AddComponent<HorizontalLayoutGroup>();
            milestone7Layout.spacing = 20;
            milestone7Layout.childAlignment = TextAnchor.MiddleLeft;
            milestone7Layout.childControlWidth = false;
            milestone7Layout.childControlHeight = false;

            var milestone7Text = CreateText(milestone7Row.transform, "Milestone7Text", "7개 완료: 오프라인 +1h", 36, TextAlignmentOptions.Left, new Vector2(450, 60));
            CreateButton(milestone7Row.transform, "Milestone7Button", "수령", 32, new Vector2(150, 60), new Color(0.2f, 0.8f, 0.2f)).GetComponent<Button>().interactable = false;

            // 4. Refresh Area
            var refreshArea = CreateEmpty(questTab.transform, "RefreshArea", new Vector2(700, 150));
            var refreshLayout = refreshArea.AddComponent<VerticalLayoutGroup>();
            refreshLayout.spacing = 15;
            refreshLayout.childAlignment = TextAnchor.UpperCenter;

            var refreshCountText = CreateText(refreshArea.transform, "RefreshCountText", "미션 재설정 (무료 0/2)", 36, TextAlignmentOptions.Center, new Vector2(700, 50));

            var buttonRow = CreateEmpty(refreshArea.transform, "ButtonRow", new Vector2(500, 80));
            var buttonLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 20;
            buttonLayout.childAlignment = TextAnchor.MiddleCenter;

            var refreshQuestButton = CreateButton(buttonRow.transform, "RefreshButton", "재설정", 36, new Vector2(240, 80));
            CreateButton(buttonRow.transform, "AdRefreshButton", "광고로 재설정", 32, new Vector2(240, 80), new Color(0.8f, 0.2f, 0.8f));

            // Controller SerializeField 자동 매핑
            var controller = questTab.GetComponent<UI.Game.QuestTabController>();
            if (controller != null)
            {
                var serializedObject = new SerializedObject(controller);

                // questCountText 매핑
                var questCountTextProp = serializedObject.FindProperty("questCountText");
                if (questCountTextProp != null)
                    questCountTextProp.objectReferenceValue = questCountText.GetComponent<TextMeshProUGUI>();

                // questListContainer 매핑
                var questListContainerProp = serializedObject.FindProperty("questListContainer");
                if (questListContainerProp != null)
                    questListContainerProp.objectReferenceValue = listContainer.transform;

                // refreshQuestButton 매핑
                var refreshQuestButtonProp = serializedObject.FindProperty("refreshQuestButton");
                if (refreshQuestButtonProp != null)
                    refreshQuestButtonProp.objectReferenceValue = refreshQuestButton.GetComponent<Button>();

                // refreshCountText 매핑
                var refreshCountTextProp = serializedObject.FindProperty("refreshCountText");
                if (refreshCountTextProp != null)
                    refreshCountTextProp.objectReferenceValue = refreshCountText.GetComponent<TextMeshProUGUI>();

                // milestone3Text 매핑
                var milestone3TextProp = serializedObject.FindProperty("milestone3Text");
                if (milestone3TextProp != null)
                    milestone3TextProp.objectReferenceValue = milestone3Text.GetComponent<TextMeshProUGUI>();

                // milestone5Text 매핑
                var milestone5TextProp = serializedObject.FindProperty("milestone5Text");
                if (milestone5TextProp != null)
                    milestone5TextProp.objectReferenceValue = milestone5Text.GetComponent<TextMeshProUGUI>();

                // milestone7Text 매핑
                var milestone7TextProp = serializedObject.FindProperty("milestone7Text");
                if (milestone7TextProp != null)
                    milestone7TextProp.objectReferenceValue = milestone7Text.GetComponent<TextMeshProUGUI>();

                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(controller);

                Debug.Log("QuestTabController SerializeField 자동 매핑 완료!");
            }

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
            var adCountText = CreateText(adSection.transform, "AdCountText", "광고 시청 (오늘 0/3)", 36, TextAlignmentOptions.Center, new Vector2(700, 50));

            var adRow1 = CreateRowWithButton(adSection.transform, "AdRow1", "1회: 크리스탈 +10", "시청", new Vector2(150, 70));
            var watchAdButton1 = adRow1.transform.Find("Button").gameObject;

            var adRow2 = CreateRowWithButton(adSection.transform, "AdRow2", "2회: 크리스탈 +14", "시청", new Vector2(150, 70));
            var watchAdButton2 = adRow2.transform.Find("Button").gameObject;

            var adRow3 = CreateRowWithButton(adSection.transform, "AdRow3", "3회: 크리스탈 +18", "시청", new Vector2(150, 70));
            var watchAdButton3 = adRow3.transform.Find("Button").gameObject;

            // 3. IAP Section (STUB)
            var iapSection = CreateSection(shopTab.transform, "IAPSection", "크리스탈 패키지 (준비중)");

            var iapSmallRow = CreateRowWithButton(iapSection.transform, "IAPSmallButtonRow", "소량: 100개 - $0.99", "준비중", new Vector2(150, 70));
            var iapSmallButton = iapSmallRow.transform.Find("Button").gameObject;
            iapSmallButton.GetComponent<Button>().interactable = false;

            var iapMediumRow = CreateRowWithButton(iapSection.transform, "IAPMediumButtonRow", "중량: 500개 - $4.99", "준비중", new Vector2(150, 70));
            var iapMediumButton = iapMediumRow.transform.Find("Button").gameObject;
            iapMediumButton.GetComponent<Button>().interactable = false;

            var iapLargeRow = CreateRowWithButton(iapSection.transform, "IAPLargeButtonRow", "대량: 1200개 - $9.99", "준비중", new Vector2(150, 70));
            var iapLargeButton = iapLargeRow.transform.Find("Button").gameObject;
            iapLargeButton.GetComponent<Button>().interactable = false;

            // 4. Slot Unlock Section
            var slotSection = CreateSection(shopTab.transform, "SlotUnlockSection", "슬롯 해금");

            var slot2Row = CreateRowWithButton(slotSection.transform, "Slot2Row", "슬롯 2: 400 크리스탈", "해금", new Vector2(150, 70));
            var unlockSlot2Button = slot2Row.transform.Find("Button").gameObject;
            var slot2CostText = CreateText(slot2Row.transform.GetChild(0).transform, "Slot2CostText", "", 0, TextAlignmentOptions.Left, Vector2.zero);
            slot2CostText.SetActive(false);

            var slot3Row = CreateRowWithButton(slotSection.transform, "Slot3Row", "슬롯 3: 2,000 크리스탈", "해금", new Vector2(150, 70));
            var unlockSlot3Button = slot3Row.transform.Find("Button").gameObject;
            var slot3CostText = CreateText(slot3Row.transform.GetChild(0).transform, "Slot3CostText", "", 0, TextAlignmentOptions.Left, Vector2.zero);
            slot3CostText.SetActive(false);

            var slot4Row = CreateRowWithButton(slotSection.transform, "Slot4Row", "슬롯 4: 4,000 크리스탈", "해금", new Vector2(150, 70));
            var unlockSlot4Button = slot4Row.transform.Find("Button").gameObject;
            var slot4CostText = CreateText(slot4Row.transform.GetChild(0).transform, "Slot4CostText", "", 0, TextAlignmentOptions.Left, Vector2.zero);
            slot4CostText.SetActive(false);

            // Controller SerializeField 자동 매핑
            var controller = shopTab.GetComponent<UI.Game.ShopTabController>();
            if (controller != null)
            {
                var serializedObject = new SerializedObject(controller);

                // Ad UI 매핑
                var watchAdButton1Prop = serializedObject.FindProperty("watchAdButton1");
                if (watchAdButton1Prop != null)
                    watchAdButton1Prop.objectReferenceValue = watchAdButton1.GetComponent<Button>();

                var watchAdButton2Prop = serializedObject.FindProperty("watchAdButton2");
                if (watchAdButton2Prop != null)
                    watchAdButton2Prop.objectReferenceValue = watchAdButton2.GetComponent<Button>();

                var watchAdButton3Prop = serializedObject.FindProperty("watchAdButton3");
                if (watchAdButton3Prop != null)
                    watchAdButton3Prop.objectReferenceValue = watchAdButton3.GetComponent<Button>();

                var adCountTextProp = serializedObject.FindProperty("adCountText");
                if (adCountTextProp != null)
                    adCountTextProp.objectReferenceValue = adCountText.GetComponent<TextMeshProUGUI>();

                // Slot Unlock UI 매핑
                var unlockSlot2ButtonProp = serializedObject.FindProperty("unlockSlot2Button");
                if (unlockSlot2ButtonProp != null)
                    unlockSlot2ButtonProp.objectReferenceValue = unlockSlot2Button.GetComponent<Button>();

                var unlockSlot3ButtonProp = serializedObject.FindProperty("unlockSlot3Button");
                if (unlockSlot3ButtonProp != null)
                    unlockSlot3ButtonProp.objectReferenceValue = unlockSlot3Button.GetComponent<Button>();

                var unlockSlot4ButtonProp = serializedObject.FindProperty("unlockSlot4Button");
                if (unlockSlot4ButtonProp != null)
                    unlockSlot4ButtonProp.objectReferenceValue = unlockSlot4Button.GetComponent<Button>();

                var slot2CostTextProp = serializedObject.FindProperty("slot2CostText");
                if (slot2CostTextProp != null)
                    slot2CostTextProp.objectReferenceValue = slot2CostText.GetComponent<TextMeshProUGUI>();

                var slot3CostTextProp = serializedObject.FindProperty("slot3CostText");
                if (slot3CostTextProp != null)
                    slot3CostTextProp.objectReferenceValue = slot3CostText.GetComponent<TextMeshProUGUI>();

                var slot4CostTextProp = serializedObject.FindProperty("slot4CostText");
                if (slot4CostTextProp != null)
                    slot4CostTextProp.objectReferenceValue = slot4CostText.GetComponent<TextMeshProUGUI>();

                // IAP UI 매핑
                var iapSmallButtonProp = serializedObject.FindProperty("iapSmallButton");
                if (iapSmallButtonProp != null)
                    iapSmallButtonProp.objectReferenceValue = iapSmallButton.GetComponent<Button>();

                var iapMediumButtonProp = serializedObject.FindProperty("iapMediumButton");
                if (iapMediumButtonProp != null)
                    iapMediumButtonProp.objectReferenceValue = iapMediumButton.GetComponent<Button>();

                var iapLargeButtonProp = serializedObject.FindProperty("iapLargeButton");
                if (iapLargeButtonProp != null)
                    iapLargeButtonProp.objectReferenceValue = iapLargeButton.GetComponent<Button>();

                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(controller);

                Debug.Log("ShopTabController SerializeField 자동 매핑 완료!");
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

            // 슬라이더와 볼륨 텍스트 찾기
            var bgmSlider = soundSection.transform.Find("BGMRow/BGMSlider").gameObject;
            var bgmVolumeText = soundSection.transform.Find("BGMRow/BGMVolumeText").gameObject;
            var sfxSlider = soundSection.transform.Find("SFXRow/SFXSlider").gameObject;
            var sfxVolumeText = soundSection.transform.Find("SFXRow/SFXVolumeText").gameObject;

            // 3. Notification Section
            var notiSection = CreateSection(settingsTab.transform, "NotificationSection", "알림");
            CreateToggleRow(notiSection.transform, "OfflineNotificationRow", "오프라인 채굴 완료:", "OfflineNotificationToggle", true);
            CreateToggleRow(notiSection.transform, "MissionNotificationRow", "일일 미션 리셋:", "MissionNotificationToggle", true);

            // 토글 찾기
            var offlineNotificationToggle = notiSection.transform.Find("OfflineNotificationRow/OfflineNotificationToggle").gameObject;
            var missionNotificationToggle = notiSection.transform.Find("MissionNotificationRow/MissionNotificationToggle").gameObject;

            // 4. Account Section
            var accountSection = CreateSection(settingsTab.transform, "AccountSection", "계정");
            var accountInfoText = CreateText(accountSection.transform, "AccountInfoText", "Google Play 연동됨", 36, TextAlignmentOptions.Center, new Vector2(700, 60));
            var logoutButton = CreateButton(accountSection.transform, "LogoutButton", "로그아웃", 40, new Vector2(400, 100), new Color(0.8f, 0.2f, 0.2f));

            // 5. Info Section
            var infoSection = CreateSection(settingsTab.transform, "InfoSection", "정보");
            var versionText = CreateText(infoSection.transform, "VersionText", "버전: 1.0.0 (MVP)", 32, TextAlignmentOptions.Center, new Vector2(700, 50));

            var linkRow = CreateEmpty(infoSection.transform, "LinkButtonRow", new Vector2(600, 80));
            var linkLayout = linkRow.AddComponent<HorizontalLayoutGroup>();
            linkLayout.spacing = 15;
            linkLayout.childAlignment = TextAnchor.MiddleCenter;

            var termsButton = CreateButton(linkRow.transform, "TermsButton", "이용약관", 28, new Vector2(180, 80));
            var privacyButton = CreateButton(linkRow.transform, "PrivacyButton", "개인정보처리방침", 24, new Vector2(180, 80));
            var supportButton = CreateButton(linkRow.transform, "SupportButton", "고객지원", 28, new Vector2(180, 80));

            // Controller SerializeField 자동 매핑
            var controller = settingsTab.GetComponent<UI.Game.SettingsTabController>();
            if (controller != null)
            {
                var serializedObject = new SerializedObject(controller);

                // Sound UI 매핑
                var bgmSliderProp = serializedObject.FindProperty("bgmSlider");
                if (bgmSliderProp != null)
                    bgmSliderProp.objectReferenceValue = bgmSlider.GetComponent<Slider>();

                var sfxSliderProp = serializedObject.FindProperty("sfxSlider");
                if (sfxSliderProp != null)
                    sfxSliderProp.objectReferenceValue = sfxSlider.GetComponent<Slider>();

                var bgmVolumeTextProp = serializedObject.FindProperty("bgmVolumeText");
                if (bgmVolumeTextProp != null)
                    bgmVolumeTextProp.objectReferenceValue = bgmVolumeText.GetComponent<TextMeshProUGUI>();

                var sfxVolumeTextProp = serializedObject.FindProperty("sfxVolumeText");
                if (sfxVolumeTextProp != null)
                    sfxVolumeTextProp.objectReferenceValue = sfxVolumeText.GetComponent<TextMeshProUGUI>();

                // Notification UI 매핑
                var offlineNotificationToggleProp = serializedObject.FindProperty("offlineNotificationToggle");
                if (offlineNotificationToggleProp != null)
                    offlineNotificationToggleProp.objectReferenceValue = offlineNotificationToggle.GetComponent<Toggle>();

                var missionNotificationToggleProp = serializedObject.FindProperty("missionNotificationToggle");
                if (missionNotificationToggleProp != null)
                    missionNotificationToggleProp.objectReferenceValue = missionNotificationToggle.GetComponent<Toggle>();

                // Account UI 매핑
                var accountInfoTextProp = serializedObject.FindProperty("accountInfoText");
                if (accountInfoTextProp != null)
                    accountInfoTextProp.objectReferenceValue = accountInfoText.GetComponent<TextMeshProUGUI>();

                var logoutButtonProp = serializedObject.FindProperty("logoutButton");
                if (logoutButtonProp != null)
                    logoutButtonProp.objectReferenceValue = logoutButton.GetComponent<Button>();

                // Info UI 매핑
                var versionTextProp = serializedObject.FindProperty("versionText");
                if (versionTextProp != null)
                    versionTextProp.objectReferenceValue = versionText.GetComponent<TextMeshProUGUI>();

                var termsButtonProp = serializedObject.FindProperty("termsButton");
                if (termsButtonProp != null)
                    termsButtonProp.objectReferenceValue = termsButton.GetComponent<Button>();

                var privacyButtonProp = serializedObject.FindProperty("privacyButton");
                if (privacyButtonProp != null)
                    privacyButtonProp.objectReferenceValue = privacyButton.GetComponent<Button>();

                var supportButtonProp = serializedObject.FindProperty("supportButton");
                if (supportButtonProp != null)
                    supportButtonProp.objectReferenceValue = supportButton.GetComponent<Button>();

                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(controller);

                Debug.Log("SettingsTabController SerializeField 자동 매핑 완료!");
            }

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

        #region Modal Creation

        private static void CreatePickaxeInfoModal()
        {
            var panel = GameObject.Find("Panel");
            if (panel == null)
            {
                Debug.LogError("Panel GameObject를 찾을 수 없습니다!");
                return;
            }

            var existing = panel.transform.Find("PickaxeInfoModal");
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("확인", "기존 PickaxeInfoModal을 삭제하고 다시 생성하시겠습니까?", "예", "아니오"))
                    return;
                DestroyImmediate(existing.gameObject);
            }

            // Modal Root (전체 화면, 어두운 배경)
            var modal = new GameObject("PickaxeInfoModal");
            modal.transform.SetParent(panel.transform, false);

            var modalRect = modal.AddComponent<RectTransform>();
            modalRect.anchorMin = Vector2.zero;
            modalRect.anchorMax = Vector2.one;
            modalRect.sizeDelta = Vector2.zero;

            // 배경 (반투명 검정)
            var bg = modal.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.8f);

            // 배경 클릭으로 닫기 가능하도록 Button 추가
            var bgButton = modal.AddComponent<Button>();
            bgButton.targetGraphic = bg;

            // Modal Panel (중앙 패널)
            var modalPanel = new GameObject("ModalPanel");
            modalPanel.transform.SetParent(modal.transform, false);

            var panelRect = modalPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(900, 1200);

            var panelBg = modalPanel.AddComponent<Image>();
            panelBg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            var panelLayout = modalPanel.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(60, 60, 60, 60);
            panelLayout.spacing = 40;
            panelLayout.childAlignment = TextAnchor.UpperCenter;
            panelLayout.childControlWidth = false;
            panelLayout.childControlHeight = false;

            // Title
            CreateText(modalPanel.transform, "TitleText", "곡괭이 정보", 60, TextAlignmentOptions.Center, new Vector2(780, 80), FontStyles.Bold);

            // Pickaxe Image
            var pickaxeImageArea = CreateEmpty(modalPanel.transform, "PickaxeImage", new Vector2(400, 400));
            var pickaxeImage = pickaxeImageArea.AddComponent<Image>();
            pickaxeImage.color = new Color(0.7f, 0.7f, 0.7f);

            // Pickaxe Level
            CreateText(modalPanel.transform, "PickaxeLevelText", "Lv 0", 52, TextAlignmentOptions.Center, new Vector2(780, 70), FontStyles.Bold);

            // Stats
            CreateText(modalPanel.transform, "AttackPowerText", "공격력: 10", 44, TextAlignmentOptions.Center, new Vector2(780, 60));
            CreateText(modalPanel.transform, "AttackSpeedText", "공격속도: 1.0", 44, TextAlignmentOptions.Center, new Vector2(780, 60));
            CreateText(modalPanel.transform, "CriticalChanceText", "크리티컬 확률: 5%", 44, TextAlignmentOptions.Center, new Vector2(780, 60));
            CreateText(modalPanel.transform, "CriticalDamageText", "크리티컬 데미지: 150%", 44, TextAlignmentOptions.Center, new Vector2(780, 60));
            var dpsText = CreateText(modalPanel.transform, "DPSText", "DPS: 10", 48, TextAlignmentOptions.Center, new Vector2(780, 70), FontStyles.Bold);
            dpsText.GetComponent<TextMeshProUGUI>().color = new Color(1f, 0.8f, 0.2f);

            // Button Row
            var buttonRow = CreateEmpty(modalPanel.transform, "ButtonRow", new Vector2(780, 120));
            var buttonLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 30;
            buttonLayout.childAlignment = TextAnchor.MiddleCenter;

            CreateButton(buttonRow.transform, "UpgradeButton", "강화", 48, new Vector2(360, 120), new Color(0.2f, 0.8f, 0.2f));
            CreateButton(buttonRow.transform, "CloseButton", "닫기", 48, new Vector2(360, 120), new Color(0.6f, 0.6f, 0.6f));

            // 초기 비활성화
            modal.SetActive(false);

            Debug.Log("PickaxeInfoModal 생성 완료!");
        }

        private static void CreateLockedSlotModal()
        {
            var panel = GameObject.Find("Panel");
            if (panel == null)
            {
                Debug.LogError("Panel GameObject를 찾을 수 없습니다!");
                return;
            }

            var existing = panel.transform.Find("LockedSlotModal");
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("확인", "기존 LockedSlotModal을 삭제하고 다시 생성하시겠습니까?", "예", "아니오"))
                    return;
                DestroyImmediate(existing.gameObject);
            }

            // Modal Root
            var modal = new GameObject("LockedSlotModal");
            modal.transform.SetParent(panel.transform, false);

            var modalRect = modal.AddComponent<RectTransform>();
            modalRect.anchorMin = Vector2.zero;
            modalRect.anchorMax = Vector2.one;
            modalRect.sizeDelta = Vector2.zero;

            var bg = modal.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.8f);

            var bgButton = modal.AddComponent<Button>();
            bgButton.targetGraphic = bg;

            // Modal Panel
            var modalPanel = new GameObject("ModalPanel");
            modalPanel.transform.SetParent(modal.transform, false);

            var panelRect = modalPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(800, 500);

            var panelBg = modalPanel.AddComponent<Image>();
            panelBg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            var panelLayout = modalPanel.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(60, 60, 60, 60);
            panelLayout.spacing = 50;
            panelLayout.childAlignment = TextAnchor.MiddleCenter;
            panelLayout.childControlWidth = false;
            panelLayout.childControlHeight = false;

            // Lock Icon (임시)
            var lockIcon = CreateEmpty(modalPanel.transform, "LockIcon", new Vector2(150, 150));
            var lockImage = lockIcon.AddComponent<Image>();
            lockImage.color = new Color(0.8f, 0.2f, 0.2f);

            // Message
            CreateText(modalPanel.transform, "MessageText", "해당 슬롯은 잠겨있습니다", 48, TextAlignmentOptions.Center, new Vector2(680, 60));

            // Button Row
            var buttonRow = CreateEmpty(modalPanel.transform, "ButtonRow", new Vector2(680, 120));
            var buttonLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 30;
            buttonLayout.childAlignment = TextAnchor.MiddleCenter;

            CreateButton(buttonRow.transform, "ShopButton", "상점", 48, new Vector2(320, 120), new Color(0.2f, 0.6f, 0.8f));
            CreateButton(buttonRow.transform, "CloseButton", "닫기", 48, new Vector2(320, 120), new Color(0.6f, 0.6f, 0.6f));

            modal.SetActive(false);

            Debug.Log("LockedSlotModal 생성 완료!");
        }

        private static void CreateMineralSelectModal()
        {
            var panel = GameObject.Find("Panel");
            if (panel == null)
            {
                Debug.LogError("Panel GameObject를 찾을 수 없습니다!");
                return;
            }

            var existing = panel.transform.Find("MineralSelectModal");
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("확인", "기존 MineralSelectModal을 삭제하고 다시 생성하시겠습니까?", "예", "아니오"))
                    return;
                DestroyImmediate(existing.gameObject);
            }

            // Modal Root
            var modal = new GameObject("MineralSelectModal");
            modal.transform.SetParent(panel.transform, false);

            var modalRect = modal.AddComponent<RectTransform>();
            modalRect.anchorMin = Vector2.zero;
            modalRect.anchorMax = Vector2.one;
            modalRect.sizeDelta = Vector2.zero;

            var bg = modal.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.8f);

            var bgButton = modal.AddComponent<Button>();
            bgButton.targetGraphic = bg;

            // Modal Panel (높이 1600)
            var modalPanel = new GameObject("ModalPanel");
            modalPanel.transform.SetParent(modal.transform, false);

            var panelRect = modalPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(1000, 1600);

            var panelBg = modalPanel.AddComponent<Image>();
            panelBg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            var panelLayout = modalPanel.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(40, 40, 40, 40);
            panelLayout.spacing = 30;
            panelLayout.childAlignment = TextAnchor.UpperCenter;
            panelLayout.childControlWidth = false;
            panelLayout.childControlHeight = false;

            // Title
            CreateText(modalPanel.transform, "TitleText", "광물 선택", 60, TextAlignmentOptions.Center, new Vector2(920, 80), FontStyles.Bold);

            // Scroll View
            var scrollView = new GameObject("ScrollView");
            scrollView.transform.SetParent(modalPanel.transform, false);

            var scrollRect = scrollView.AddComponent<RectTransform>();
            scrollRect.sizeDelta = new Vector2(920, 1200);

            var scrollComp = scrollView.AddComponent<ScrollRect>();
            scrollComp.horizontal = false;
            scrollComp.vertical = true;
            scrollComp.movementType = ScrollRect.MovementType.Clamped;
            scrollComp.scrollSensitivity = 30;

            var mask = scrollView.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var scrollBg = scrollView.AddComponent<Image>();
            scrollBg.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);

            // Content
            var content = new GameObject("Content");
            content.transform.SetParent(scrollView.transform, false);

            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 0);

            var contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 20;
            contentLayout.padding = new RectOffset(20, 20, 20, 20);
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlWidth = false;
            contentLayout.childControlHeight = false;

            var sizeFitter = content.AddComponent<ContentSizeFitter>();
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollComp.content = contentRect;

            // Sample Mineral Items (7개)
            var minerals = new[] { "약한 돌", "돌", "석탄", "구리", "철", "은", "금" };
            var hps = new[] { 25, 50, 300, 1500, 8000, 40000, 200000 };
            var golds = new[] { 2, 5, 30, 140, 700, 3500, 17500 };

            for (int i = 0; i < minerals.Length; i++)
            {
                var item = CreateEmpty(content.transform, $"MineralItem_{minerals[i]}", new Vector2(880, 180));
                var itemLayout = item.AddComponent<HorizontalLayoutGroup>();
                itemLayout.spacing = 20;
                itemLayout.padding = new RectOffset(30, 30, 30, 30);
                itemLayout.childAlignment = TextAnchor.MiddleLeft;
                itemLayout.childControlWidth = false;
                itemLayout.childControlHeight = false;

                var itemBg = item.AddComponent<Image>();
                itemBg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

                // Mineral Icon
                var icon = CreateEmpty(item.transform, "Icon", new Vector2(120, 120));
                var iconImage = icon.AddComponent<Image>();
                iconImage.color = new Color(0.6f, 0.4f + i * 0.08f, 0.2f);

                // Info
                var info = CreateEmpty(item.transform, "Info", new Vector2(550, 120));
                var infoLayout = info.AddComponent<VerticalLayoutGroup>();
                infoLayout.spacing = 10;
                infoLayout.childAlignment = TextAnchor.MiddleLeft;

                CreateText(info.transform, "NameText", minerals[i], 40, TextAlignmentOptions.Left, new Vector2(550, 50), FontStyles.Bold);
                CreateText(info.transform, "StatsText", $"HP: {hps[i]:N0}  골드: {golds[i]:N0}", 32, TextAlignmentOptions.Left, new Vector2(550, 40));

                // Select Button
                CreateButton(item.transform, "SelectButton", "선택", 36, new Vector2(150, 120));
            }

            // Close Button
            CreateButton(modalPanel.transform, "CloseButton", "닫기", 48, new Vector2(400, 120), new Color(0.6f, 0.6f, 0.6f));

            modal.SetActive(false);

            Debug.Log("MineralSelectModal 생성 완료!");
        }

        #endregion
    }
}
