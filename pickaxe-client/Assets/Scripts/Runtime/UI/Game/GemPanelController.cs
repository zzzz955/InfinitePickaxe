using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using InfinitePickaxe.Client.Core;
using InfinitePickaxe.Client.Metadata;
using InfinitePickaxe.Client.Net;
using Infinitepickaxe;

namespace InfinitePickaxe.Client.UI.Game
{
    public sealed class GemPanelController : MonoBehaviour
    {
        private enum GemFilter
        {
            All = 0,
            AttackSpeed = 1,
            CritRate = 2,      // 프로토콜과 일치
            CritDmg = 3        // 프로토콜과 일치
        }

        private enum GemMode
        {
            Fusion = 0,
            Convert = 1,
            Discard = 2
        }

        [Serializable]
        private sealed class GemSlotView
        {
            public Button button;
            public Image iconImage;
            public TextMeshProUGUI nameText;
            public TextMeshProUGUI tierText;
            public TextMeshProUGUI roleText;
            public GameObject emptyState;
            public GameObject filledState;

            public void SetEmpty(string roleLabel, string placeholder)
            {
                if (roleText != null) roleText.text = roleLabel;
                if (nameText != null) nameText.text = placeholder;
                if (tierText != null) tierText.text = string.Empty;
                if (emptyState != null) emptyState.SetActive(true);
                if (filledState != null) filledState.SetActive(false);
                if (iconImage != null) iconImage.sprite = null;
            }

            public void SetGem(string roleLabel, string displayName, string tierLabel, Sprite icon)
            {
                if (roleText != null) roleText.text = roleLabel;
                if (nameText != null) nameText.text = displayName;
                if (tierText != null) tierText.text = tierLabel;
                if (emptyState != null) emptyState.SetActive(false);
                if (filledState != null) filledState.SetActive(true);
                if (iconImage != null)
                {
                    iconImage.sprite = icon;
                    iconImage.color = icon != null ? Color.white : new Color(1f, 1f, 1f, 0.6f);
                }
            }
        }

        [Header("필터 탭")]
        [SerializeField] private Button filterAllButton;
        [SerializeField] private Button filterAttackSpeedButton;
        [SerializeField] private Button filterCritRateButton;
        [SerializeField] private Button filterCritDmgButton;
        [SerializeField] private Color filterSelectedColor = new Color(0.22f, 0.22f, 0.22f, 0.95f);
        [SerializeField] private Color filterUnselectedColor = new Color(0.1f, 0.1f, 0.1f, 0.7f);

        [Header("모드 전환")]
        [SerializeField] private Button fusionModeButton;
        [SerializeField] private Button convertModeButton;
        [SerializeField] private Color modeSelectedColor = new Color(0.22f, 0.22f, 0.22f, 0.95f);
        [SerializeField] private Color modeUnselectedColor = new Color(0.1f, 0.1f, 0.1f, 0.7f);

        [Header("보석 그리드")]
        [SerializeField] private Transform gridContent;
        [SerializeField] private GemGridItemView gemItemTemplate;
        [SerializeField] private int initialCapacity = 48;
        [SerializeField] private int maxCapacity = 128;
        [SerializeField] private int capacityStep = 8;
        [SerializeField] private Button expandRowButton;
        [SerializeField] private TextMeshProUGUI expandCostText;
        [SerializeField] private TextMeshProUGUI capacityText;

        [Header("합성 패널")]
        [SerializeField] private GameObject fusionRoot;
        [SerializeField] private GemSlotView fusionBaseSlot;
        [SerializeField] private GemSlotView fusionMaterialSlot;
        [SerializeField] private GemSlotView fusionMaterialSlot2;
        [SerializeField] private GemSlotView fusionResultSlot;
        [SerializeField] private TextMeshProUGUI fusionChanceText;
        [SerializeField] private TextMeshProUGUI fusionWarningText;
        [SerializeField] private Button fusionButton;
        [SerializeField] private Button autoSynthesisButton;
        [SerializeField] private TextMeshProUGUI autoSynthesisButtonText;

        [Header("전환 패널")]
        [SerializeField] private GameObject convertRoot;
        [SerializeField] private GemSlotView convertBaseSlot;
        [SerializeField] private GemSlotView convertResultSlot;
        [SerializeField] private TextMeshProUGUI convertInfoText;
        [SerializeField] private Button convertRandomButton;
        [SerializeField] private Button convertFixedAttackSpeedButton;
        [SerializeField] private Button convertFixedCritRateButton;
        [SerializeField] private Button convertFixedCritDmgButton;
        [Header("분해 패널")]
        [SerializeField] private Button discardModeButton;
        [SerializeField] private GameObject discardRoot;
        [SerializeField] private TextMeshProUGUI discardSelectedCountText;
        [SerializeField] private TextMeshProUGUI discardRewardPreviewText;
        [SerializeField] private Button discardButton;
        [Header("모달")]
        [SerializeField] private GemSynthesisResultModalController synthesisResultModal;

        [Header("자동 합성 확인 모달")]
        [SerializeField] private GameObject autoSynthesisConfirmModal;
        [SerializeField] private TextMeshProUGUI autoSynthesisConfirmCountText;
        [SerializeField] private TextMeshProUGUI autoSynthesisConfirmChanceText;
        [SerializeField] private TextMeshProUGUI autoSynthesisConfirmFailText;
        [SerializeField] private TextMeshProUGUI autoSynthesisConfirmSuccessText;
        [SerializeField] private Button autoSynthesisConfirmButton;
        [SerializeField] private TextMeshProUGUI autoSynthesisConfirmButtonText;
        [SerializeField] private Button autoSynthesisCancelButton;

        [Header("보석 전환 확인 모달")]
        [SerializeField] private GameObject conversionConfirmModal;
        [SerializeField] private TextMeshProUGUI conversionConfirmCostText;
        [SerializeField] private TextMeshProUGUI conversionConfirmCurrentCrystalText;
        [SerializeField] private TextMeshProUGUI conversionConfirmTypeText;
        [SerializeField] private Button conversionConfirmButton;
        [SerializeField] private TextMeshProUGUI conversionConfirmButtonText;
        [SerializeField] private Button conversionCancelButton;
        [Header("보석 분해 확인 모달")]
        [SerializeField] private GameObject discardConfirmModal;
        [SerializeField] private Transform discardConfirmGridContent;
        [SerializeField] private Image discardConfirmItemTemplate;
        [SerializeField] private TextMeshProUGUI discardConfirmRewardText;
        [SerializeField] private Button discardConfirmButton;
        [SerializeField] private TextMeshProUGUI discardConfirmButtonText;
        [SerializeField] private Button discardConfirmCancelButton;

        [Header("스텁 데이터")]
        [SerializeField] private bool useStubData = true;
        [SerializeField] private int stubGemCount = 24;

        private readonly List<GemUIData> allGems = new List<GemUIData>();
        private readonly Dictionary<string, GemUIData> gemByInstanceId = new Dictionary<string, GemUIData>();
        private readonly List<GemGridItemView> gridItems = new List<GemGridItemView>();
        private readonly List<string> slotGemInstanceIds = new List<string>();
        private int currentCapacity;
        private MessageHandler messageHandler;
        private GemStateCache gemCache;
        private readonly GemMetaResolver metaResolver = new GemMetaResolver();
        private bool subscribed;
        private bool cacheSubscribed;
        private uint currentCrystal;
        private bool hasCrystalInfo;
        private UserResourceCache resourceCache;
        private bool resourceSubscribed;
        private GemFilter currentFilter = GemFilter.All;
        private GemMode currentMode = GemMode.Fusion;
        private string selectedBaseGemId;
        private string selectedMaterialGemId;
        private string selectedMaterialGemId2;
        private string selectedConvertGemId;
        private Infinitepickaxe.GemType? selectedConvertTarget;
        private readonly HashSet<string> selectedDiscardGemIds = new HashSet<string>();
        private Infinitepickaxe.GemGrade? pendingAutoSynthesisGrade;
        private string pendingConvertGemId;
        private Infinitepickaxe.GemType? pendingConvertTarget;
        private bool pendingConvertFixed;
        private List<string> pendingDiscardRequestIds;
        private readonly List<Image> discardConfirmItems = new List<Image>();

        private void Awake()
        {
            ApplyMetaInventoryConfig();
            currentCapacity = Mathf.Clamp(initialCapacity, capacityStep, maxCapacity);
            BindFilterButtons();
            BindModeButtons();
            BindActionButtons();
            BindSlotButtons();
            gemCache = GemStateCache.Instance;
            AutoBindSynthesisResultModal();
            SetupAutoSynthesisConfirmModalButtons();
            SetupConversionConfirmModalButtons();
            SetupDiscardConfirmModalButtons();

            if (useStubData)
            {
                BuildStubGems();
            }
            else
            {
                LoadGemsFromCache();
            }

            EnsureGridItems(currentCapacity);
            RebuildGrid();
            SetMode(currentMode);
        }

        private void Start()
        {
            SubscribeMessageHandler();
            SubscribeCache();
            if (!useStubData)
            {
                RequestGemListIfNeeded();
            }
        }

        private void BindFilterButtons()
        {
            if (filterAllButton != null)
            {
                filterAllButton.onClick.RemoveAllListeners();
                filterAllButton.onClick.AddListener(() => SetFilter(GemFilter.All));
            }

            if (filterAttackSpeedButton != null)
            {
                filterAttackSpeedButton.onClick.RemoveAllListeners();
                filterAttackSpeedButton.onClick.AddListener(() => SetFilter(GemFilter.AttackSpeed));
            }

            if (filterCritRateButton != null)
            {
                filterCritRateButton.onClick.RemoveAllListeners();
                filterCritRateButton.onClick.AddListener(() => SetFilter(GemFilter.CritRate));
            }

            if (filterCritDmgButton != null)
            {
                filterCritDmgButton.onClick.RemoveAllListeners();
                filterCritDmgButton.onClick.AddListener(() => SetFilter(GemFilter.CritDmg));
            }
        }

        private void BindModeButtons()
        {
            if (fusionModeButton != null)
            {
                fusionModeButton.onClick.RemoveAllListeners();
                fusionModeButton.onClick.AddListener(() => SetMode(GemMode.Fusion));
            }

            if (convertModeButton != null)
            {
                convertModeButton.onClick.RemoveAllListeners();
                convertModeButton.onClick.AddListener(() => SetMode(GemMode.Convert));
            }

            if (discardModeButton != null)
            {
                discardModeButton.onClick.RemoveAllListeners();
                discardModeButton.onClick.AddListener(() => SetMode(GemMode.Discard));
            }
        }

        private void BindActionButtons()
        {
            if (expandRowButton != null)
            {
                expandRowButton.onClick.RemoveAllListeners();
                expandRowButton.onClick.AddListener(OnExpandRowClicked);
            }

            if (fusionButton != null)
            {
                fusionButton.onClick.RemoveAllListeners();
                fusionButton.onClick.AddListener(OnFusionClicked);
            }

            if (autoSynthesisButton != null)
            {
                autoSynthesisButton.onClick.RemoveAllListeners();
                autoSynthesisButton.onClick.AddListener(OnAutoSynthesisClicked);
            }

            if (convertRandomButton != null)
            {
                convertRandomButton.onClick.RemoveAllListeners();
                convertRandomButton.onClick.AddListener(OnConvertRandomClicked);
            }

            if (convertFixedAttackSpeedButton != null)
            {
                convertFixedAttackSpeedButton.onClick.RemoveAllListeners();
                convertFixedAttackSpeedButton.onClick.AddListener(() => OnConvertFixedClicked(Infinitepickaxe.GemType.AttackSpeed));
            }

            if (convertFixedCritRateButton != null)
            {
                convertFixedCritRateButton.onClick.RemoveAllListeners();
                convertFixedCritRateButton.onClick.AddListener(() => OnConvertFixedClicked(Infinitepickaxe.GemType.CritRate));
            }

            if (convertFixedCritDmgButton != null)
            {
                convertFixedCritDmgButton.onClick.RemoveAllListeners();
                convertFixedCritDmgButton.onClick.AddListener(() => OnConvertFixedClicked(Infinitepickaxe.GemType.CritDmg));
            }

            if (discardButton != null)
            {
                discardButton.onClick.RemoveAllListeners();
                discardButton.onClick.AddListener(OnDiscardClicked);
            }
        }

        private void SetupAutoSynthesisConfirmModalButtons()
        {
            if (autoSynthesisConfirmModal == null) return;

            var backgroundButton = autoSynthesisConfirmModal.GetComponent<Button>();
            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveAllListeners();
                backgroundButton.onClick.AddListener(CloseAutoSynthesisConfirmModal);
            }

            var modalPanel = autoSynthesisConfirmModal.transform.Find("ModalPanel");
            if (modalPanel != null)
            {
                var panelButton = modalPanel.GetComponent<Button>();
                if (panelButton == null)
                {
                    panelButton = modalPanel.gameObject.AddComponent<Button>();
                    panelButton.transition = Selectable.Transition.None;
                }
                panelButton.onClick.RemoveAllListeners();
            }

            if (autoSynthesisCancelButton != null)
            {
                autoSynthesisCancelButton.onClick.RemoveAllListeners();
                autoSynthesisCancelButton.onClick.AddListener(CloseAutoSynthesisConfirmModal);
            }

            if (autoSynthesisConfirmButton != null)
            {
                autoSynthesisConfirmButton.onClick.RemoveAllListeners();
                autoSynthesisConfirmButton.onClick.AddListener(OnConfirmAutoSynthesis);
            }
        }

        private void SetupConversionConfirmModalButtons()
        {
            if (conversionConfirmModal == null) return;

            var backgroundButton = conversionConfirmModal.GetComponent<Button>();
            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveAllListeners();
                backgroundButton.onClick.AddListener(CloseConversionConfirmModal);
            }

            var modalPanel = conversionConfirmModal.transform.Find("ModalPanel");
            if (modalPanel != null)
            {
                var panelButton = modalPanel.GetComponent<Button>();
                if (panelButton == null)
                {
                    panelButton = modalPanel.gameObject.AddComponent<Button>();
                    panelButton.transition = Selectable.Transition.None;
                }
                panelButton.onClick.RemoveAllListeners();
            }

            if (conversionCancelButton != null)
            {
                conversionCancelButton.onClick.RemoveAllListeners();
                conversionCancelButton.onClick.AddListener(CloseConversionConfirmModal);
            }

            if (conversionConfirmButton != null)
            {
                conversionConfirmButton.onClick.RemoveAllListeners();
                conversionConfirmButton.onClick.AddListener(OnConfirmConversion);
            }
        }

        private void SetupDiscardConfirmModalButtons()
        {
            if (discardConfirmModal == null) return;

            var backgroundButton = discardConfirmModal.GetComponent<Button>();
            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveAllListeners();
                backgroundButton.onClick.AddListener(CloseDiscardConfirmModal);
            }

            var modalPanel = discardConfirmModal.transform.Find("ModalPanel");
            if (modalPanel != null)
            {
                var panelButton = modalPanel.GetComponent<Button>();
                if (panelButton == null)
                {
                    panelButton = modalPanel.gameObject.AddComponent<Button>();
                    panelButton.transition = Selectable.Transition.None;
                }
                panelButton.onClick.RemoveAllListeners();
            }

            if (discardConfirmCancelButton != null)
            {
                discardConfirmCancelButton.onClick.RemoveAllListeners();
                discardConfirmCancelButton.onClick.AddListener(CloseDiscardConfirmModal);
            }

            if (discardConfirmButton != null)
            {
                discardConfirmButton.onClick.RemoveAllListeners();
                discardConfirmButton.onClick.AddListener(OnConfirmDiscard);
            }
        }

        private void BindSlotButtons()
        {
            BindSlotButton(fusionBaseSlot, ClearFusionBase);
            BindSlotButton(fusionMaterialSlot, ClearFusionMaterial);
            BindSlotButton(fusionMaterialSlot2, ClearFusionMaterial2);
            BindSlotButton(convertBaseSlot, ClearConvertBase);
        }

        private void BindSlotButton(GemSlotView slot, Action onClick)
        {
            if (slot == null || slot.button == null) return;
            slot.button.onClick.RemoveAllListeners();
            slot.button.onClick.AddListener(() => onClick?.Invoke());
        }

        /// <summary>
        /// 스텁 데이터 생성 (임시)
        /// TODO: GemStateCache에서 실제 데이터 가져오기
        /// </summary>
        private void BuildStubGems()
        {
            allGems.Clear();
            gemByInstanceId.Clear();

            int id = 1;
            var grades = new[] {
                Infinitepickaxe.GemGrade.Legendary,
                Infinitepickaxe.GemGrade.Hero,
                Infinitepickaxe.GemGrade.Epic,
                Infinitepickaxe.GemGrade.Rare,
                Infinitepickaxe.GemGrade.Common
            };

            for (int i = 0; i < stubGemCount; i++)
            {
                var gem = new GemUIData
                {
                    GemInstanceId = $"stub_gem_{id}",
                    GemId = (uint)id++,
                    Grade = grades[i % grades.Length],
                    Type = (Infinitepickaxe.GemType)((i % 3) + 1), // AttackSpeed=1, CritRate=2, CritDmg=3
                    Name = $"보석 #{id}",
                    IconName = "gem_default",
                    StatMultiplier = 500, // 5.00%
                    AcquiredAt = 0
                };
                allGems.Add(gem);
                gemByInstanceId[gem.GemInstanceId] = gem;
            }
        }

        private void EnsureGridItems(int requiredCount)
        {
            if (gridContent == null)
            {
                Debug.LogWarning("GemPanelController: GemGridContent가 없습니다.");
                return;
            }

            if (gemItemTemplate == null)
            {
                var templateTf = gridContent.Find("GemItemTemplate");
                if (templateTf != null)
                {
                    gemItemTemplate = templateTf.GetComponent<GemGridItemView>();
                }
            }

            if (gemItemTemplate == null)
            {
                Debug.LogWarning("GemPanelController: GemItemTemplate가 없습니다.");
                return;
            }

            while (gridItems.Count < requiredCount)
            {
                var instance = Instantiate(gemItemTemplate, gridContent);
                instance.gameObject.SetActive(true);
                gridItems.Add(instance);
            }

            for (int i = 0; i < gridItems.Count; i++)
            {
                gridItems[i].gameObject.SetActive(i < requiredCount);
                gridItems[i].Bind(i, OnGridItemClicked);
            }
        }

        private void SetFilter(GemFilter filter)
        {
            currentFilter = filter;
            UpdateFilterButtons();
            RebuildGrid();
        }

        private void SetMode(GemMode mode)
        {
            currentMode = mode;

            if (fusionRoot != null) fusionRoot.SetActive(mode == GemMode.Fusion);
            if (convertRoot != null) convertRoot.SetActive(mode == GemMode.Convert);
            if (discardRoot != null) discardRoot.SetActive(mode == GemMode.Discard);

            UpdateModeButtons();
            ClearSelectionOnModeChange();
            UpdateSelectionUI();
        }

        private void RebuildGrid()
        {
            EnsureGridItems(currentCapacity);
            if (gridItems.Count < currentCapacity)
            {
                Debug.LogWarning("GemPanelController: GemItemTemplate 연결을 확인하세요.");
                return;
            }
            slotGemInstanceIds.Clear();

            var filtered = GetFilteredGems();
            for (int i = 0; i < currentCapacity; i++)
            {
                var view = gridItems[i];
                if (i < filtered.Count)
                {
                    var gem = filtered[i];
                    slotGemInstanceIds.Add(gem.GemInstanceId);
                    view.SetData(GetGemDisplayName(gem), GetGradeLabel(gem.Grade), GetGemIcon(gem));
                }
                else
                {
                    slotGemInstanceIds.Add(null);
                    view.SetEmpty();
                }
            }

            UpdateGridSelectionStates();
            UpdateCapacityText();
            UpdateExpandButtonState();
        }

        private List<GemUIData> GetFilteredGems()
        {
            return allGems
                .Where(g => IsFilterMatch(g, currentFilter))
                .OrderByDescending(g => g.Grade)
                .ThenByDescending(g => g.GemId)
                .ToList();
        }

        private bool IsFilterMatch(GemUIData gem, GemFilter filter)
        {
            return filter switch
            {
                GemFilter.AttackSpeed => gem.Type == Infinitepickaxe.GemType.AttackSpeed,
                GemFilter.CritRate => gem.Type == Infinitepickaxe.GemType.CritRate,
                GemFilter.CritDmg => gem.Type == Infinitepickaxe.GemType.CritDmg,
                _ => true
            };
        }

        private void OnGridItemClicked(int index)
        {
            if (index < 0 || index >= slotGemInstanceIds.Count) return;
            var gemInstanceId = slotGemInstanceIds[index];
            if (string.IsNullOrEmpty(gemInstanceId)) return;

            if (currentMode == GemMode.Fusion)
            {
                SelectFusionGem(gemInstanceId);
            }
            else if (currentMode == GemMode.Convert)
            {
                SelectConvertGem(gemInstanceId);
            }
            else
            {
                ToggleDiscardGem(gemInstanceId);
            }

            UpdateSelectionUI();
        }

        private void SelectFusionGem(string gemInstanceId)
        {
            if (string.IsNullOrEmpty(gemInstanceId)) return;
            if (selectedBaseGemId == gemInstanceId ||
                selectedMaterialGemId == gemInstanceId ||
                selectedMaterialGemId2 == gemInstanceId)
            {
                return;
            }

            var gem = GetGem(gemInstanceId);
            if (gem == null) return;

            if (string.IsNullOrEmpty(selectedBaseGemId))
            {
                selectedBaseGemId = gemInstanceId;
                return;
            }

            var baseGem = GetGem(selectedBaseGemId);
            if (baseGem == null)
            {
                selectedBaseGemId = gemInstanceId;
                selectedMaterialGemId = null;
                selectedMaterialGemId2 = null;
                return;
            }

            if (gem.Grade != baseGem.Grade)
            {
                selectedBaseGemId = gemInstanceId;
                selectedMaterialGemId = null;
                selectedMaterialGemId2 = null;
                return;
            }

            if (string.IsNullOrEmpty(selectedMaterialGemId))
            {
                selectedMaterialGemId = gemInstanceId;
                return;
            }

            if (string.IsNullOrEmpty(selectedMaterialGemId2))
            {
                selectedMaterialGemId2 = gemInstanceId;
            }
        }

        private void SelectConvertGem(string gemInstanceId)
        {
            selectedConvertGemId = gemInstanceId;
            selectedConvertTarget = null;
        }

        private void ToggleDiscardGem(string gemInstanceId)
        {
            if (string.IsNullOrEmpty(gemInstanceId)) return;
            if (gemCache != null && gemCache.IsEquipped(gemInstanceId))
            {
                Debug.LogWarning("GemPanelController: 장착된 보석은 분해할 수 없습니다.");
                return;
            }

            if (!selectedDiscardGemIds.Add(gemInstanceId))
            {
                selectedDiscardGemIds.Remove(gemInstanceId);
            }
        }

        private void ClearFusionBase()
        {
            selectedBaseGemId = null;
            selectedMaterialGemId = null;
            selectedMaterialGemId2 = null;
            UpdateSelectionUI();
        }

        private void ClearFusionMaterial()
        {
            if (!string.IsNullOrEmpty(selectedMaterialGemId2))
            {
                selectedMaterialGemId = selectedMaterialGemId2;
                selectedMaterialGemId2 = null;
            }
            else
            {
                selectedMaterialGemId = null;
            }
            UpdateSelectionUI();
        }

        private void ClearFusionMaterial2()
        {
            selectedMaterialGemId2 = null;
            UpdateSelectionUI();
        }

        private void ClearConvertBase()
        {
            selectedConvertGemId = null;
            selectedConvertTarget = null;
            UpdateSelectionUI();
        }

        private void ClearDiscardSelection()
        {
            selectedDiscardGemIds.Clear();
            pendingDiscardRequestIds = null;
            UpdateSelectionUI();
        }

        private void ClearSelectionOnModeChange()
        {
            switch (currentMode)
            {
                case GemMode.Fusion:
                    selectedConvertGemId = null;
                    selectedConvertTarget = null;
                    selectedDiscardGemIds.Clear();
                    pendingDiscardRequestIds = null;
                    break;
                case GemMode.Convert:
                    selectedBaseGemId = null;
                    selectedMaterialGemId = null;
                    selectedMaterialGemId2 = null;
                    selectedDiscardGemIds.Clear();
                    pendingDiscardRequestIds = null;
                    break;
                case GemMode.Discard:
                    selectedBaseGemId = null;
                    selectedMaterialGemId = null;
                    selectedMaterialGemId2 = null;
                    selectedConvertGemId = null;
                    selectedConvertTarget = null;
                    break;
            }
        }

        private void UpdateSelectionUI()
        {
            UpdateFusionSlots();
            UpdateConvertSlots();
            UpdateGridSelectionStates();
            UpdateConvertButtons();
            UpdateDiscardPanel();
        }

        private void UpdateFusionSlots()
        {
            var baseGem = !string.IsNullOrEmpty(selectedBaseGemId) ? GetGem(selectedBaseGemId) : null;
            var materialGem = !string.IsNullOrEmpty(selectedMaterialGemId) ? GetGem(selectedMaterialGemId) : null;
            var materialGem2 = !string.IsNullOrEmpty(selectedMaterialGemId2) ? GetGem(selectedMaterialGemId2) : null;
            var normalizedGrade = baseGem != null ? NormalizeSynthesisGrade(baseGem.Grade) : Infinitepickaxe.GemGrade.Unknown;

            UpdateFusionSlotView(fusionBaseSlot, "기준", baseGem);
            UpdateFusionSlotView(fusionMaterialSlot, "재료1", materialGem);
            UpdateFusionSlotView(fusionMaterialSlot2, "재료2", materialGem2);

            if (baseGem != null && normalizedGrade != Infinitepickaxe.GemGrade.Legendary)
            {
                var nextGrade = GetNextGrade(normalizedGrade);
                fusionResultSlot?.SetGem("합성 결과", "랜덤 보석", GetGradeLabel(nextGrade), null);
            }
            else if (baseGem != null)
            {
                fusionResultSlot?.SetEmpty("합성 결과", "결과 없음");
            }
            else
            {
                fusionResultSlot?.SetEmpty("합성 결과", "결과 없음");
            }

            if (fusionChanceText != null)
            {
                fusionChanceText.text = baseGem == null
                    ? "성공 확률: -"
                    : normalizedGrade == Infinitepickaxe.GemGrade.Legendary
                        ? "성공 확률: -"
                        : $"성공 확률: {GetFusionChance(normalizedGrade)}%";
            }

            if (fusionWarningText != null)
            {
                fusionWarningText.text = "실패 시 2개 소멸, 1개 유지\n동일 등급 3개 필요";
            }

            if (fusionButton != null)
            {
                bool canFuse = baseGem != null
                    && materialGem != null
                    && materialGem2 != null
                    && baseGem.Grade != Infinitepickaxe.GemGrade.Legendary
                    && materialGem.Grade == baseGem.Grade
                    && materialGem2.Grade == baseGem.Grade;
                fusionButton.interactable = canFuse;
            }

            if (autoSynthesisButton != null)
            {
                bool canAutoSynthesis = baseGem != null
                    && normalizedGrade != Infinitepickaxe.GemGrade.Legendary
                    && HasSynthesisMetadata(normalizedGrade)
                    && GetAvailableAutoSynthesisCount(normalizedGrade) >= 3;
                autoSynthesisButton.interactable = canAutoSynthesis;
            }
        }

        private void UpdateFusionSlotView(GemSlotView slot, string roleLabel, GemUIData gem)
        {
            if (slot == null) return;

            if (gem == null)
            {
                slot.SetEmpty(roleLabel, "보석 선택");
                return;
            }

            var icon = GetGemIcon(gem);
            slot.SetGem(roleLabel, GetGemDisplayName(gem), GetGradeLabel(gem.Grade), icon);
        }
        private void UpdateConvertSlots()
        {
            var baseGem = !string.IsNullOrEmpty(selectedConvertGemId) ? GetGem(selectedConvertGemId) : null;
            if (baseGem == null)
            {
                convertBaseSlot?.SetEmpty("현재 보석", "보석 선택");
                convertResultSlot?.SetEmpty("전환 결과", "보석 선택");
                if (convertInfoText != null) convertInfoText.text = "랜덤 전환: 현재 타입 제외";
                return;
            }

            convertBaseSlot?.SetGem("현재 보석", GetGemDisplayName(baseGem), GetGradeLabel(baseGem.Grade), GetGemIcon(baseGem));

            if (selectedConvertTarget.HasValue)
            {
                var targetType = selectedConvertTarget.Value;
                convertResultSlot?.SetGem("전환 결과", GetGemDisplayName(targetType, baseGem.Grade), GetGradeLabel(baseGem.Grade), null);
                if (convertInfoText != null) convertInfoText.text = "확정 전환: 선택 타입으로 변환";
            }
            else
            {
                convertResultSlot?.SetGem("전환 결과", "랜덤 전환", GetGradeLabel(baseGem.Grade), null);
                if (convertInfoText != null) convertInfoText.text = "랜덤 전환: 현재 타입 제외";
            }
        }

        private void UpdateGridSelectionStates()
        {
            for (int i = 0; i < gridItems.Count; i++)
            {
                if (i >= slotGemInstanceIds.Count) continue;
                var gemInstanceId = slotGemInstanceIds[i];
                var role = GemSelectionRole.None;
                if (!string.IsNullOrEmpty(gemInstanceId))
                {
                    if (currentMode == GemMode.Discard)
                    {
                        if (selectedDiscardGemIds.Contains(gemInstanceId))
                        {
                            role = GemSelectionRole.Discard;
                        }
                    }
                    else if (!string.IsNullOrEmpty(selectedBaseGemId) && gemInstanceId == selectedBaseGemId)
                    {
                        role = GemSelectionRole.Base;
                    }
                    else if (!string.IsNullOrEmpty(selectedMaterialGemId) && gemInstanceId == selectedMaterialGemId)
                    {
                        role = GemSelectionRole.Material;
                    }
                    else if (!string.IsNullOrEmpty(selectedMaterialGemId2) && gemInstanceId == selectedMaterialGemId2)
                    {
                        role = GemSelectionRole.Material2;
                    }
                    else if (!string.IsNullOrEmpty(selectedConvertGemId) && gemInstanceId == selectedConvertGemId)
                    {
                        role = GemSelectionRole.Convert;
                    }
                }

                gridItems[i].SetSelectionRole(role);
            }
        }

        private void UpdateFilterButtons()
        {
            UpdateButtonState(filterAllButton, currentFilter == GemFilter.All, filterSelectedColor, filterUnselectedColor);
            UpdateButtonState(filterAttackSpeedButton, currentFilter == GemFilter.AttackSpeed, filterSelectedColor, filterUnselectedColor);
            UpdateButtonState(filterCritRateButton, currentFilter == GemFilter.CritRate, filterSelectedColor, filterUnselectedColor);
            UpdateButtonState(filterCritDmgButton, currentFilter == GemFilter.CritDmg, filterSelectedColor, filterUnselectedColor);
        }

        private void UpdateModeButtons()
        {
            UpdateButtonState(fusionModeButton, currentMode == GemMode.Fusion, modeSelectedColor, modeUnselectedColor);
            UpdateButtonState(convertModeButton, currentMode == GemMode.Convert, modeSelectedColor, modeUnselectedColor);
            UpdateButtonState(discardModeButton, currentMode == GemMode.Discard, modeSelectedColor, modeUnselectedColor);
        }

        private void UpdateConvertButtons()
        {
            var baseGem = !string.IsNullOrEmpty(selectedConvertGemId) ? GetGem(selectedConvertGemId) : null;
            bool hasBase = baseGem != null;

            if (convertRandomButton != null) convertRandomButton.interactable = hasBase;
            if (convertFixedAttackSpeedButton != null)
                convertFixedAttackSpeedButton.interactable = hasBase && baseGem.Type != Infinitepickaxe.GemType.AttackSpeed;
            if (convertFixedCritRateButton != null)
                convertFixedCritRateButton.interactable = hasBase && baseGem.Type != Infinitepickaxe.GemType.CritRate;
            if (convertFixedCritDmgButton != null)
                convertFixedCritDmgButton.interactable = hasBase && baseGem.Type != Infinitepickaxe.GemType.CritDmg;
        }

        private void UpdateDiscardPanel()
        {
            if (discardRoot != null)
            {
                discardRoot.SetActive(currentMode == GemMode.Discard);
            }

            int selectedCount = selectedDiscardGemIds.Count;
            if (discardSelectedCountText != null)
            {
                discardSelectedCountText.text = $"선택: {selectedCount}";
            }

            if (discardRewardPreviewText != null)
            {
                uint totalReward = GetTotalDiscardReward(selectedDiscardGemIds);
                discardRewardPreviewText.text = $"예상 보상: {totalReward} 크리스탈";
            }

            if (discardButton != null)
            {
                discardButton.interactable = selectedCount > 0;
            }
        }

        private void UpdateButtonState(Button button, bool selected, Color selectedColor, Color unselectedColor)
        {
            if (button == null) return;
            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = selected ? selectedColor : unselectedColor;
            }
            button.interactable = !selected;
        }

        private void OnFusionClicked()
        {
            if (messageHandler == null)
            {
                Debug.LogWarning("GemPanelController: MessageHandler가 없습니다.");
                return;
            }

            if (string.IsNullOrEmpty(selectedBaseGemId) ||
                string.IsNullOrEmpty(selectedMaterialGemId) ||
                string.IsNullOrEmpty(selectedMaterialGemId2))
            {
                Debug.LogWarning("GemPanelController: 합성 재료가 부족합니다.");
                return;
            }

            var baseGem = GetGem(selectedBaseGemId);
            if (baseGem == null || baseGem.Grade == Infinitepickaxe.GemGrade.Legendary)
            {
                Debug.LogWarning("GemPanelController: 최고 등급은 합성이 불가합니다.");
                return;
            }

            if (gemCache != null &&
                (gemCache.IsEquipped(selectedBaseGemId) ||
                 gemCache.IsEquipped(selectedMaterialGemId) ||
                 gemCache.IsEquipped(selectedMaterialGemId2)))
            {
                Debug.LogWarning("GemPanelController: 장착된 보석은 합성할 수 없습니다.");
                return;
            }

            messageHandler.RequestGemSynthesis(selectedBaseGemId, selectedMaterialGemId, selectedMaterialGemId2);
        }

        private void OnAutoSynthesisClicked()
        {
            if (messageHandler == null)
            {
                Debug.LogWarning("GemPanelController: MessageHandler가 없습니다.");
                return;
            }

            var baseGem = !string.IsNullOrEmpty(selectedBaseGemId) ? GetGem(selectedBaseGemId) : null;
            if (baseGem == null)
            {
                Debug.LogWarning("GemPanelController: 자동 합성 기준 보석이 없습니다.");
                return;
            }

            var normalizedGrade = NormalizeSynthesisGrade(baseGem.Grade);
            if (normalizedGrade == Infinitepickaxe.GemGrade.Legendary)
            {
                Debug.LogWarning("GemPanelController: 최고 등급은 자동 합성이 불가합니다.");
                return;
            }

            if (!HasSynthesisMetadata(normalizedGrade))
            {
                Debug.LogWarning("GemPanelController: 합성 메타데이터가 없습니다.");
                return;
            }

            if (GetAvailableAutoSynthesisCount(normalizedGrade) < 3)
            {
                Debug.LogWarning("GemPanelController: 자동 합성 재료가 부족합니다.");
                return;
            }

            OpenAutoSynthesisConfirmModal(normalizedGrade);
        }

        private void OnConvertRandomClicked()
        {
            selectedConvertTarget = null;
            UpdateConvertSlots();
            UpdateConvertButtons();

            if (messageHandler == null)
            {
                Debug.LogWarning("GemPanelController: MessageHandler가 없습니다.");
                return;
            }

            var baseGem = !string.IsNullOrEmpty(selectedConvertGemId) ? GetGem(selectedConvertGemId) : null;
            if (baseGem == null)
            {
                Debug.LogWarning("GemPanelController: 전환 대상 보석이 없습니다.");
                return;
            }

            OpenConversionConfirmModal(baseGem, null, false);
        }

        private void OnConvertFixedClicked(Infinitepickaxe.GemType targetType)
        {
            var baseGem = !string.IsNullOrEmpty(selectedConvertGemId) ? GetGem(selectedConvertGemId) : null;
            if (baseGem == null) return;
            if (baseGem.Type == targetType) return;

            selectedConvertTarget = targetType;
            UpdateConvertSlots();
            UpdateConvertButtons();

            if (messageHandler == null)
            {
                Debug.LogWarning("GemPanelController: MessageHandler가 없습니다.");
                return;
            }

            OpenConversionConfirmModal(baseGem, targetType, true);
        }

        private void OnDiscardClicked()
        {
            ValidateSelections();
            if (selectedDiscardGemIds.Count == 0)
            {
                Debug.LogWarning("GemPanelController: 분해할 보석이 없습니다.");
                return;
            }

            OpenDiscardConfirmModal();
        }

        private void OpenAutoSynthesisConfirmModal(Infinitepickaxe.GemGrade grade)
        {
            if (autoSynthesisConfirmModal == null) return;

            pendingAutoSynthesisGrade = grade;
            int availableCount = GetAvailableAutoSynthesisCount(grade);
            int attemptCount = availableCount / 3;
            int consumeCount = attemptCount * 3;
            int successRate = GetFusionChance(grade);

            if (autoSynthesisConfirmCountText != null)
            {
                autoSynthesisConfirmCountText.text = $"총 {consumeCount}개의 보석이 자동으로 합성됩니다.";
            }

            if (autoSynthesisConfirmChanceText != null)
            {
                autoSynthesisConfirmChanceText.text = $"성공 확률: {successRate}%";
            }

            if (autoSynthesisConfirmFailText != null)
            {
                autoSynthesisConfirmFailText.text = "실패 시 합성에 사용된 보석 중 1개만 남습니다.";
            }

            if (autoSynthesisConfirmSuccessText != null)
            {
                autoSynthesisConfirmSuccessText.text = "성공 시 랜덤한 타입의 상위 등급 보석을 획득합니다.";
            }

            if (autoSynthesisConfirmButton != null)
            {
                autoSynthesisConfirmButton.interactable = true;
            }

            if (autoSynthesisConfirmButtonText != null)
            {
                autoSynthesisConfirmButtonText.text = "확인";
            }

            autoSynthesisConfirmModal.SetActive(true);
            autoSynthesisConfirmModal.transform.SetAsLastSibling();
        }

        private void CloseAutoSynthesisConfirmModal()
        {
            pendingAutoSynthesisGrade = null;
            if (autoSynthesisConfirmModal != null)
            {
                autoSynthesisConfirmModal.SetActive(false);
            }
        }

        private void OnConfirmAutoSynthesis()
        {
            if (messageHandler == null)
            {
                Debug.LogWarning("GemPanelController: MessageHandler가 없습니다.");
                return;
            }

            if (!pendingAutoSynthesisGrade.HasValue)
            {
                CloseAutoSynthesisConfirmModal();
                return;
            }

            var grade = pendingAutoSynthesisGrade.Value;
            if (!HasSynthesisMetadata(grade) || GetAvailableAutoSynthesisCount(grade) < 3)
            {
                Debug.LogWarning("GemPanelController: 자동 합성 조건이 충족되지 않습니다.");
                CloseAutoSynthesisConfirmModal();
                return;
            }

            messageHandler.RequestGemAutoSynthesis(grade, 0);
            CloseAutoSynthesisConfirmModal();
        }

        private void OpenConversionConfirmModal(GemUIData baseGem, Infinitepickaxe.GemType? targetType, bool useFixedCost)
        {
            if (conversionConfirmModal == null || baseGem == null) return;

            if (!TryGetConversionCost(baseGem, useFixedCost, out var cost))
            {
                Debug.LogWarning("GemPanelController: 전환 비용 메타데이터를 찾지 못했습니다.");
                return;
            }

            pendingConvertGemId = baseGem.GemInstanceId;
            pendingConvertTarget = targetType;
            pendingConvertFixed = useFixedCost;

            UpdateConversionConfirmModalTexts(baseGem, targetType, useFixedCost, cost);

            conversionConfirmModal.SetActive(true);
            conversionConfirmModal.transform.SetAsLastSibling();
        }

        private void CloseConversionConfirmModal()
        {
            pendingConvertGemId = null;
            pendingConvertTarget = null;
            pendingConvertFixed = false;
            if (conversionConfirmModal != null)
            {
                conversionConfirmModal.SetActive(false);
            }
        }

        private void OnConfirmConversion()
        {
            if (messageHandler == null)
            {
                Debug.LogWarning("GemPanelController: MessageHandler가 없습니다.");
                return;
            }

            if (string.IsNullOrEmpty(pendingConvertGemId))
            {
                CloseConversionConfirmModal();
                return;
            }

            var baseGem = GetGem(pendingConvertGemId);
            if (baseGem == null)
            {
                Debug.LogWarning("GemPanelController: 전환 대상 보석이 없습니다.");
                CloseConversionConfirmModal();
                return;
            }

            if (!TryGetConversionCost(baseGem, pendingConvertFixed, out var cost))
            {
                Debug.LogWarning("GemPanelController: 전환 비용 메타데이터를 찾지 못했습니다.");
                CloseConversionConfirmModal();
                return;
            }

            bool canAfford = cost == 0 || (hasCrystalInfo && currentCrystal >= cost);
            if (!canAfford)
            {
                UpdateConversionConfirmModalTexts(baseGem, pendingConvertTarget, pendingConvertFixed, cost);
                return;
            }

            var targetType = pendingConvertTarget ?? GetRandomConvertTarget(baseGem.Type);
            messageHandler.RequestGemConversion(pendingConvertGemId, targetType, pendingConvertFixed);
            CloseConversionConfirmModal();
        }

        private void UpdateConversionConfirmModalTexts(GemUIData baseGem, Infinitepickaxe.GemType? targetType, bool useFixedCost, uint cost)
        {
            if (conversionConfirmCostText != null)
            {
                conversionConfirmCostText.text = $"필요 크리스탈: {cost}";
            }

            if (conversionConfirmCurrentCrystalText != null)
            {
                conversionConfirmCurrentCrystalText.text = hasCrystalInfo
                    ? $"보유: {currentCrystal}"
                    : "보유: -";
            }

            if (conversionConfirmTypeText != null)
            {
                conversionConfirmTypeText.text = useFixedCost && targetType.HasValue
                    ? $"선택한 타입({GetTypeLabel(targetType.Value)})으로 전환됩니다."
                    : "랜덤 타입으로 전환됩니다.";
            }

            bool canAfford = cost == 0 || (hasCrystalInfo && currentCrystal >= cost);
            if (conversionConfirmButton != null)
            {
                conversionConfirmButton.interactable = canAfford;
            }

            if (conversionConfirmButtonText != null)
            {
                conversionConfirmButtonText.text = canAfford ? "확인" : "크리스탈 부족";
            }
        }

        private void OpenDiscardConfirmModal()
        {
            if (discardConfirmModal == null) return;

            ValidateSelections();
            if (selectedDiscardGemIds.Count == 0)
            {
                Debug.LogWarning("GemPanelController: 분해할 보석이 없습니다.");
                return;
            }

            UpdateDiscardConfirmModal();

            if (discardConfirmButton != null)
            {
                discardConfirmButton.interactable = true;
            }

            if (discardConfirmButtonText != null)
            {
                discardConfirmButtonText.text = "확인";
            }

            discardConfirmModal.SetActive(true);
            discardConfirmModal.transform.SetAsLastSibling();
        }

        private void CloseDiscardConfirmModal()
        {
            if (discardConfirmModal != null)
            {
                discardConfirmModal.SetActive(false);
            }
        }

        private void OnConfirmDiscard()
        {
            if (messageHandler == null)
            {
                Debug.LogWarning("GemPanelController: MessageHandler가 없습니다.");
                return;
            }

            ValidateSelections();
            var requestIds = selectedDiscardGemIds.Where(id => !string.IsNullOrEmpty(id)).ToList();
            if (requestIds.Count == 0)
            {
                CloseDiscardConfirmModal();
                return;
            }

            if (gemCache != null)
            {
                var equippedIds = requestIds.Where(id => gemCache.IsEquipped(id)).ToList();
                if (equippedIds.Count > 0)
                {
                    foreach (var id in equippedIds)
                    {
                        selectedDiscardGemIds.Remove(id);
                    }
                }
                requestIds.RemoveAll(id => gemCache.IsEquipped(id));
                if (requestIds.Count == 0)
                {
                    Debug.LogWarning("GemPanelController: 장착된 보석은 분해할 수 없습니다.");
                    CloseDiscardConfirmModal();
                    UpdateSelectionUI();
                    return;
                }
            }

            pendingDiscardRequestIds = requestIds;
            messageHandler.RequestGemDiscard(requestIds);
            CloseDiscardConfirmModal();
        }

        private void UpdateDiscardConfirmModal()
        {
            if (discardConfirmModal == null) return;

            var gems = selectedDiscardGemIds
                .Select(GetGem)
                .Where(gem => gem != null)
                .ToList();

            uint totalReward = GetTotalDiscardReward(selectedDiscardGemIds);
            if (discardConfirmRewardText != null)
            {
                discardConfirmRewardText.text = $"획득 크리스탈: {totalReward}";
            }

            EnsureDiscardConfirmItems(gems);
        }

        private void EnsureDiscardConfirmItems(IReadOnlyList<GemUIData> gems)
        {
            if (discardConfirmGridContent == null || discardConfirmItemTemplate == null) return;
            if (!discardConfirmGridContent.gameObject.scene.IsValid())
            {
                Debug.LogWarning("GemPanelController: 분해 확인 모달이 씬에 없습니다.");
                return;
            }

            ConfigureDiscardGridLayout();

            if (discardConfirmItemTemplate.gameObject.activeSelf)
            {
                discardConfirmItemTemplate.gameObject.SetActive(false);
            }

            int required = gems.Count;
            while (discardConfirmItems.Count < required)
            {
                var instance = Instantiate(discardConfirmItemTemplate, discardConfirmGridContent);
                instance.gameObject.SetActive(true);
                instance.preserveAspect = true;
                instance.rectTransform.sizeDelta = new Vector2(96f, 96f);
                discardConfirmItems.Add(instance);
            }

            for (int i = 0; i < discardConfirmItems.Count; i++)
            {
                var item = discardConfirmItems[i];
                if (i < required)
                {
                    var icon = GetGemIcon(gems[i]);
                    item.sprite = icon;
                    item.color = icon != null ? Color.white : new Color(1f, 1f, 1f, 0.6f);
                    item.gameObject.SetActive(true);
                }
                else
                {
                    item.gameObject.SetActive(false);
                }
            }
        }

        private void ConfigureDiscardGridLayout()
        {
            if (discardConfirmGridContent == null) return;

            var grid = discardConfirmGridContent.GetComponent<GridLayoutGroup>();
            if (grid == null)
            {
                grid = discardConfirmGridContent.gameObject.AddComponent<GridLayoutGroup>();
            }

            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6;
            grid.cellSize = new Vector2(96f, 96f);
            grid.spacing = new Vector2(8f, 8f);
            grid.childAlignment = TextAnchor.UpperLeft;
        }


        private void OnExpandRowClicked()
        {
            if (messageHandler == null)
            {
                Debug.LogWarning("GemPanelController: MessageHandler가 없습니다.");
                return;
            }

            if (maxCapacity > 0 && currentCapacity >= maxCapacity) return;

            messageHandler.RequestGemInventoryExpand();
        }

        private void UpdateExpandButtonState()
        {
            if (expandRowButton != null)
            {
                bool canExpand = maxCapacity == 0 || currentCapacity < maxCapacity;
                expandRowButton.interactable = canExpand;
            }

            if (expandCostText != null)
            {
                bool canExpand = maxCapacity == 0 || currentCapacity < maxCapacity;
                expandCostText.gameObject.SetActive(canExpand);
                if (canExpand)
                {
                    expandCostText.text = metaResolver.ExpandCost > 0
                        ? $"필요 크리스탈: {metaResolver.ExpandCost}"
                        : "필요 크리스탈: -";
                }
            }
        }

        private void UpdateCapacityText()
        {
            if (capacityText != null)
            {
                int usedCount = allGems.Count;
                capacityText.text = currentCapacity > 0
                    ? $"{usedCount}/{currentCapacity}"
                    : $"{usedCount}";
            }
        }

        private void ApplyMetaInventoryConfig()
        {
            if (metaResolver.BaseCapacity > 0) initialCapacity = (int)metaResolver.BaseCapacity;
            if (metaResolver.MaxCapacity > 0) maxCapacity = (int)metaResolver.MaxCapacity;
            if (metaResolver.ExpandStep > 0) capacityStep = (int)metaResolver.ExpandStep;
        }

        private void LoadGemsFromCache()
        {
            if (gemCache == null) return;

            allGems.Clear();
            gemByInstanceId.Clear();

            foreach (var gem in gemCache.GetInventoryGems())
            {
                var uiData = GemUIData.FromProtocol(gem);
                if (uiData == null || string.IsNullOrEmpty(uiData.GemInstanceId)) continue;
                allGems.Add(uiData);
                gemByInstanceId[uiData.GemInstanceId] = uiData;
            }

            if (gemCache.InventoryCapacity > 0)
            {
                currentCapacity = (int)gemCache.InventoryCapacity;
            }

            int maxCap = maxCapacity > 0 ? maxCapacity : currentCapacity;
            if (maxCap > 0)
            {
                currentCapacity = Mathf.Clamp(currentCapacity, capacityStep, maxCap);
            }

            ValidateSelections();
        }

        private void ValidateSelections()
        {
            if (!string.IsNullOrEmpty(selectedBaseGemId) && !gemByInstanceId.ContainsKey(selectedBaseGemId))
            {
                selectedBaseGemId = null;
            }

            if (!string.IsNullOrEmpty(selectedMaterialGemId) && !gemByInstanceId.ContainsKey(selectedMaterialGemId))
            {
                selectedMaterialGemId = null;
            }

            if (!string.IsNullOrEmpty(selectedMaterialGemId2) && !gemByInstanceId.ContainsKey(selectedMaterialGemId2))
            {
                selectedMaterialGemId2 = null;
            }

            if (!string.IsNullOrEmpty(selectedConvertGemId) && !gemByInstanceId.ContainsKey(selectedConvertGemId))
            {
                selectedConvertGemId = null;
                selectedConvertTarget = null;
            }

            if (selectedDiscardGemIds.Count > 0)
            {
                selectedDiscardGemIds.RemoveWhere(id => string.IsNullOrEmpty(id) || !gemByInstanceId.ContainsKey(id));
            }
        }

        private void AutoBindSynthesisResultModal()
        {
            if (synthesisResultModal != null)
            {
                if (!synthesisResultModal.gameObject.scene.IsValid())
                {
                    var existing = GameObject.Find("GemSynthesisResultModal");
                    if (existing != null)
                    {
                        synthesisResultModal = existing.GetComponent<GemSynthesisResultModalController>();
                    }
                    else
                    {
                        var instance = Instantiate(synthesisResultModal.gameObject, transform.root);
                        instance.name = "GemSynthesisResultModal";
                        instance.SetActive(false);
                        synthesisResultModal = instance.GetComponent<GemSynthesisResultModalController>();
                    }
                }
                return;
            }

            var modalObj = GameObject.Find("GemSynthesisResultModal");
            if (modalObj == null)
            {
                var prefab = Resources.Load<GameObject>("UI/GemSynthesisResultModal");
                if (prefab != null)
                {
                    var instance = Instantiate(prefab, transform.root);
                    instance.name = "GemSynthesisResultModal";
                    instance.SetActive(false);
                    modalObj = instance;
                }
            }

            if (modalObj != null)
            {
                synthesisResultModal = modalObj.GetComponent<GemSynthesisResultModalController>();
            }
        }

        private void SubscribeMessageHandler()
        {
            if (subscribed) return;

            messageHandler = MessageHandler.Instance;
            if (messageHandler == null)
            {
                Debug.LogWarning("GemPanelController: MessageHandler가 없습니다.");
                return;
            }

            messageHandler.OnGemListResponse += HandleGemListResponse;
            messageHandler.OnGemSynthesisResult += HandleGemSynthesisResult;
            messageHandler.OnGemAutoSynthesisResult += HandleGemAutoSynthesisResult;
            messageHandler.OnGemConversionResult += HandleGemConversionResult;
            messageHandler.OnGemDiscardResult += HandleGemDiscardResult;
            messageHandler.OnGemInventoryExpandResult += HandleGemInventoryExpandResult;
            messageHandler.OnCurrencyUpdate += HandleCurrencyUpdate;
            subscribed = true;
        }

        private void SubscribeCache()
        {
            if (cacheSubscribed) return;

            gemCache = GemStateCache.Instance;
            if (gemCache != null)
            {
                gemCache.OnInventoryChanged += HandleInventoryChanged;
                cacheSubscribed = true;
            }

            if (!resourceSubscribed)
            {
                resourceCache = UserResourceCache.Instance;
                if (resourceCache != null)
                {
                    resourceCache.OnChanged += HandleResourceCacheChanged;
                    resourceSubscribed = true;
                    ApplyResourceCache();
                }
            }
        }

        private void OnDestroy()
        {
            if (cacheSubscribed && gemCache != null)
            {
                gemCache.OnInventoryChanged -= HandleInventoryChanged;
                cacheSubscribed = false;
            }

            if (resourceSubscribed && resourceCache != null)
            {
                resourceCache.OnChanged -= HandleResourceCacheChanged;
                resourceSubscribed = false;
            }

            if (!subscribed || messageHandler == null) return;

            messageHandler.OnGemListResponse -= HandleGemListResponse;
            messageHandler.OnGemSynthesisResult -= HandleGemSynthesisResult;
            messageHandler.OnGemAutoSynthesisResult -= HandleGemAutoSynthesisResult;
            messageHandler.OnGemConversionResult -= HandleGemConversionResult;
            messageHandler.OnGemDiscardResult -= HandleGemDiscardResult;
            messageHandler.OnGemInventoryExpandResult -= HandleGemInventoryExpandResult;
            messageHandler.OnCurrencyUpdate -= HandleCurrencyUpdate;
            subscribed = false;
        }

        private void RequestGemListIfNeeded()
        {
            if (messageHandler == null) return;
            if (gemCache == null || !gemCache.HasData)
            {
                messageHandler.RequestGemList();
            }
        }

        private void HandleInventoryChanged()
        {
            if (useStubData) return;
            LoadGemsFromCache();
            EnsureGridItems(currentCapacity);
            RebuildGrid();
            UpdateSelectionUI();
        }

        private void HandleGemListResponse(GemListResponse response)
        {
            if (useStubData) return;
            LoadGemsFromCache();
            EnsureGridItems(currentCapacity);
            RebuildGrid();
            UpdateSelectionUI();
        }

        private void HandleGemSynthesisResult(GemSynthesisResult result)
        {
            if (result == null || !result.Success)
            {
                if (result != null)
                {
                    Debug.LogWarning($"GemPanelController: 합성 실패 ({result.ErrorCode})");
                }
                return;
            }

            synthesisResultModal?.ShowSynthesisResult(result);
            ClearFusionBase();
        }

        private void HandleGemAutoSynthesisResult(GemAutoSynthesisResult result)
        {
            if (result == null || !result.Success)
            {
                if (result != null)
                {
                    Debug.LogWarning($"GemPanelController: 자동 합성 실패 ({result.ErrorCode})");
                }
                return;
            }

            synthesisResultModal?.ShowAutoSynthesisResult(result);
            ClearFusionBase();
        }

        private void HandleGemConversionResult(GemConversionResult result)
        {
            if (result == null || !result.Success)
            {
                if (result != null)
                {
                    Debug.LogWarning($"GemPanelController: 전환 실패 ({result.ErrorCode})");
                }
                return;
            }

            ClearConvertBase();
        }

        private void HandleGemDiscardResult(GemDiscardResult result)
        {
            if (result == null || !result.Success)
            {
                if (result != null)
                {
                    Debug.LogWarning($"GemPanelController: 분해 실패 ({result.ErrorCode})");
                }
                pendingDiscardRequestIds = null;
                return;
            }

            if (pendingDiscardRequestIds != null)
            {
                foreach (var id in pendingDiscardRequestIds)
                {
                    selectedDiscardGemIds.Remove(id);
                }
            }

            pendingDiscardRequestIds = null;
            CloseDiscardConfirmModal();
            UpdateSelectionUI();
        }

        private void HandleGemInventoryExpandResult(GemInventoryExpandResult result)
        {
            if (result == null || !result.Success) return;
            LoadGemsFromCache();
            EnsureGridItems(currentCapacity);
            RebuildGrid();
            UpdateSelectionUI();
        }

        private void HandleCurrencyUpdate(CurrencyUpdate update)
        {
            ApplyResourceCache();
        }

        private void HandleResourceCacheChanged()
        {
            ApplyResourceCache();
        }

        private void ApplyResourceCache()
        {
            if (resourceCache == null) return;

            if (resourceCache.Crystal.HasValue)
            {
                currentCrystal = resourceCache.Crystal.Value;
                hasCrystalInfo = true;
            }

            if (conversionConfirmModal != null && conversionConfirmModal.activeSelf)
            {
                RefreshConversionConfirmModal();
            }
        }

        private Sprite GetGemIcon(GemUIData gem)
        {
            if (gem == null) return null;
            if (!string.IsNullOrEmpty(gem.IconName))
            {
                return GemSpriteLoader.GetGemSpriteByName(gem.IconName);
            }
            return GemSpriteLoader.GetGemSprite(gem.GemId);
        }

        private Infinitepickaxe.GemType GetRandomConvertTarget(Infinitepickaxe.GemType currentType)
        {
            var candidates = new List<Infinitepickaxe.GemType>
            {
                Infinitepickaxe.GemType.AttackSpeed,
                Infinitepickaxe.GemType.CritRate,
                Infinitepickaxe.GemType.CritDmg
            };
            candidates.Remove(currentType);
            if (candidates.Count == 0) return currentType;
            int index = UnityEngine.Random.Range(0, candidates.Count);
            return candidates[index];
        }

        private string GetGradeKey(Infinitepickaxe.GemGrade grade)
        {
            grade = NormalizeSynthesisGrade(grade);
            return grade switch
            {
                Infinitepickaxe.GemGrade.Common => "COMMON",
                Infinitepickaxe.GemGrade.Rare => "RARE",
                Infinitepickaxe.GemGrade.Epic => "EPIC",
                Infinitepickaxe.GemGrade.Hero => "HERO",
                Infinitepickaxe.GemGrade.Legendary => "LEGENDARY",
                _ => string.Empty
            };
        }

        private int GetFusionChance(Infinitepickaxe.GemGrade grade)
        {
            grade = NormalizeSynthesisGrade(grade);
            var gradeKey = GetGradeKey(grade);
            if (!string.IsNullOrEmpty(gradeKey) && metaResolver.TryGetSynthesisRule(gradeKey, out var rule))
            {
                return Mathf.RoundToInt(rule.SuccessRatePercent / 100f);
            }

            return grade switch
            {
                Infinitepickaxe.GemGrade.Common => 100,
                Infinitepickaxe.GemGrade.Rare => 70,
                Infinitepickaxe.GemGrade.Epic => 50,
                Infinitepickaxe.GemGrade.Hero => 30,
                _ => 0
            };
        }

        private uint GetTotalDiscardReward(IEnumerable<string> gemInstanceIds)
        {
            if (gemInstanceIds == null) return 0;
            uint total = 0;
            foreach (var gemInstanceId in gemInstanceIds)
            {
                if (string.IsNullOrEmpty(gemInstanceId)) continue;
                var gem = GetGem(gemInstanceId);
                if (gem == null) continue;
                if (TryGetDiscardReward(gem, out var reward))
                {
                    total += reward;
                }
            }
            return total;
        }

        private bool TryGetDiscardReward(GemUIData gem, out uint reward)
        {
            reward = 0;
            if (gem == null) return false;
            if (!metaResolver.TryGetDefinition(gem.GemId, out var def)) return false;
            if (!metaResolver.TryGetDiscardReward(def.GradeId, out var meta)) return false;
            reward = meta.CrystalReward;
            return true;
        }


        private GemUIData GetGem(string gemInstanceId)
        {
            return gemByInstanceId.TryGetValue(gemInstanceId, out var gem) ? gem : null;
        }

        private string GetGemDisplayName(GemUIData gem)
        {
            if (gem == null) return string.Empty;
            if (!string.IsNullOrEmpty(gem.Name)) return gem.Name;
            return GetGemDisplayName(gem.Type, gem.Grade);
        }

        private string GetGemDisplayName(Infinitepickaxe.GemType type, Infinitepickaxe.GemGrade grade)
        {
            return $"{GetTypeLabel(type)} 보석";
        }

        private string GetTypeLabel(Infinitepickaxe.GemType type)
        {
            return type switch
            {
                Infinitepickaxe.GemType.AttackSpeed => "공격속도",
                Infinitepickaxe.GemType.CritRate => "크확",
                Infinitepickaxe.GemType.CritDmg => "크뎀",
                _ => "보석"
            };
        }

        private string GetGradeLabel(Infinitepickaxe.GemGrade grade)
        {
            return grade switch
            {
                Infinitepickaxe.GemGrade.Common => "커먼",
                Infinitepickaxe.GemGrade.Rare => "레어",
                Infinitepickaxe.GemGrade.Epic => "에픽",
                Infinitepickaxe.GemGrade.Hero => "히어로",
                Infinitepickaxe.GemGrade.Legendary => "레전드",
                _ => "커먼"
            };
        }

        private Infinitepickaxe.GemGrade GetNextGrade(Infinitepickaxe.GemGrade grade)
        {
            grade = NormalizeSynthesisGrade(grade);
            return grade switch
            {
                Infinitepickaxe.GemGrade.Common => Infinitepickaxe.GemGrade.Rare,
                Infinitepickaxe.GemGrade.Rare => Infinitepickaxe.GemGrade.Epic,
                Infinitepickaxe.GemGrade.Epic => Infinitepickaxe.GemGrade.Hero,
                Infinitepickaxe.GemGrade.Hero => Infinitepickaxe.GemGrade.Legendary,
                _ => Infinitepickaxe.GemGrade.Legendary
            };
        }

        private Infinitepickaxe.GemGrade NormalizeSynthesisGrade(Infinitepickaxe.GemGrade grade)
        {
            return grade == Infinitepickaxe.GemGrade.Unknown
                ? Infinitepickaxe.GemGrade.Common
                : grade;
        }

        private void RefreshConversionConfirmModal()
        {
            if (conversionConfirmModal == null || !conversionConfirmModal.activeSelf) return;
            if (string.IsNullOrEmpty(pendingConvertGemId)) return;

            var baseGem = GetGem(pendingConvertGemId);
            if (baseGem == null) return;

            if (TryGetConversionCost(baseGem, pendingConvertFixed, out var cost))
            {
                UpdateConversionConfirmModalTexts(baseGem, pendingConvertTarget, pendingConvertFixed, cost);
            }
        }

        private bool TryGetConversionCost(GemUIData gem, bool useFixedCost, out uint cost)
        {
            cost = 0;
            if (gem == null) return false;
            if (!metaResolver.TryGetDefinition(gem.GemId, out var def)) return false;
            if (!metaResolver.TryGetConversionCost(def.GradeId, out var meta)) return false;
            cost = useFixedCost ? meta.FixedCost : meta.RandomCost;
            return true;
        }

        private int GetAvailableAutoSynthesisCount(Infinitepickaxe.GemGrade grade)
        {
            return allGems.Count(g => NormalizeSynthesisGrade(g.Grade) == grade);
        }

        private bool HasSynthesisMetadata(Infinitepickaxe.GemGrade grade)
        {
            var gradeKey = GetGradeKey(grade);
            return !string.IsNullOrEmpty(gradeKey) && metaResolver.TryGetSynthesisRule(gradeKey, out _);
        }

    }
}
