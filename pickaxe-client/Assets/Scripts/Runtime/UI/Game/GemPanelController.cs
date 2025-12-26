using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InfinitePickaxe.Client.UI.Game
{
    public sealed class GemPanelController : MonoBehaviour
    {
        private enum GemFilter
        {
            All = 0,
            AttackSpeed = 1,
            CritChance = 2,
            CritDamage = 3
        }

        private enum GemMode
        {
            Fusion = 0,
            Convert = 1
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
        [SerializeField] private Button filterCritChanceButton;
        [SerializeField] private Button filterCritDamageButton;
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
        [SerializeField] private GemSlotView fusionResultSlot;
        [SerializeField] private TextMeshProUGUI fusionChanceText;
        [SerializeField] private TextMeshProUGUI fusionWarningText;
        [SerializeField] private Button fusionButton;

        [Header("전환 패널")]
        [SerializeField] private GameObject convertRoot;
        [SerializeField] private GemSlotView convertBaseSlot;
        [SerializeField] private GemSlotView convertResultSlot;
        [SerializeField] private TextMeshProUGUI convertInfoText;
        [SerializeField] private Button convertRandomButton;
        [SerializeField] private Button convertFixedAttackSpeedButton;
        [SerializeField] private Button convertFixedCritChanceButton;
        [SerializeField] private Button convertFixedCritDamageButton;

        [Header("스텁 데이터")]
        [SerializeField] private bool useStubData = true;
        [SerializeField] private int stubGemCount = 24;

        private readonly List<GemData> allGems = new List<GemData>();
        private readonly Dictionary<int, GemData> gemById = new Dictionary<int, GemData>();
        private readonly List<GemGridItemView> gridItems = new List<GemGridItemView>();
        private readonly List<int?> slotGemIds = new List<int?>();
        private int currentCapacity;
        private GemFilter currentFilter = GemFilter.All;
        private GemMode currentMode = GemMode.Fusion;
        private int? selectedBaseGemId;
        private int? selectedMaterialGemId;
        private int? selectedConvertGemId;
        private GemType? selectedConvertTarget;

        private void Awake()
        {
            currentCapacity = Mathf.Clamp(initialCapacity, capacityStep, maxCapacity);
            BindFilterButtons();
            BindModeButtons();
            BindActionButtons();
            BindSlotButtons();

            if (useStubData)
            {
                BuildStubGems();
            }

            EnsureGridItems(currentCapacity);
            RebuildGrid();
            SetMode(currentMode);
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

            if (filterCritChanceButton != null)
            {
                filterCritChanceButton.onClick.RemoveAllListeners();
                filterCritChanceButton.onClick.AddListener(() => SetFilter(GemFilter.CritChance));
            }

            if (filterCritDamageButton != null)
            {
                filterCritDamageButton.onClick.RemoveAllListeners();
                filterCritDamageButton.onClick.AddListener(() => SetFilter(GemFilter.CritDamage));
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

            if (convertRandomButton != null)
            {
                convertRandomButton.onClick.RemoveAllListeners();
                convertRandomButton.onClick.AddListener(OnConvertRandomClicked);
            }

            if (convertFixedAttackSpeedButton != null)
            {
                convertFixedAttackSpeedButton.onClick.RemoveAllListeners();
                convertFixedAttackSpeedButton.onClick.AddListener(() => OnConvertFixedClicked(GemType.AttackSpeed));
            }

            if (convertFixedCritChanceButton != null)
            {
                convertFixedCritChanceButton.onClick.RemoveAllListeners();
                convertFixedCritChanceButton.onClick.AddListener(() => OnConvertFixedClicked(GemType.CritChance));
            }

            if (convertFixedCritDamageButton != null)
            {
                convertFixedCritDamageButton.onClick.RemoveAllListeners();
                convertFixedCritDamageButton.onClick.AddListener(() => OnConvertFixedClicked(GemType.CritDamage));
            }
        }

        private void BindSlotButtons()
        {
            BindSlotButton(fusionBaseSlot, ClearFusionBase);
            BindSlotButton(fusionMaterialSlot, ClearFusionMaterial);
            BindSlotButton(convertBaseSlot, ClearConvertBase);
        }

        private void BindSlotButton(GemSlotView slot, Action onClick)
        {
            if (slot == null || slot.button == null) return;
            slot.button.onClick.RemoveAllListeners();
            slot.button.onClick.AddListener(() => onClick?.Invoke());
        }

        private void BuildStubGems()
        {
            allGems.Clear();
            gemById.Clear();

            int id = 1;
            var tiers = new[] { GemTier.Legend, GemTier.Epic, GemTier.Rare, GemTier.Uncommon, GemTier.Common };
            for (int i = 0; i < stubGemCount; i++)
            {
                var gem = new GemData
                {
                    Id = id++,
                    Tier = tiers[i % tiers.Length],
                    Type = (GemType)(i % 3)
                };
                allGems.Add(gem);
                gemById[gem.Id] = gem;
            }
        }

        private void EnsureGridItems(int requiredCount)
        {
            if (gridContent == null || gemItemTemplate == null) return;

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

            UpdateModeButtons();
            ClearSelectionOnModeChange();
            UpdateSelectionUI();
        }

        private void RebuildGrid()
        {
            EnsureGridItems(currentCapacity);
            slotGemIds.Clear();

            var filtered = GetFilteredGems();
            for (int i = 0; i < currentCapacity; i++)
            {
                var view = gridItems[i];
                if (i < filtered.Count)
                {
                    var gem = filtered[i];
                    slotGemIds.Add(gem.Id);
                    view.SetData(GetGemDisplayName(gem), GetTierLabel(gem.Tier), null);
                }
                else
                {
                    slotGemIds.Add(null);
                    view.SetEmpty();
                }
            }

            UpdateGridSelectionStates();
            UpdateCapacityText();
            UpdateExpandButtonState();
        }

        private List<GemData> GetFilteredGems()
        {
            return allGems
                .Where(g => IsFilterMatch(g, currentFilter))
                .OrderByDescending(g => g.Tier)
                .ThenByDescending(g => g.Id)
                .ToList();
        }

        private bool IsFilterMatch(GemData gem, GemFilter filter)
        {
            return filter switch
            {
                GemFilter.AttackSpeed => gem.Type == GemType.AttackSpeed,
                GemFilter.CritChance => gem.Type == GemType.CritChance,
                GemFilter.CritDamage => gem.Type == GemType.CritDamage,
                _ => true
            };
        }

        private void OnGridItemClicked(int index)
        {
            if (index < 0 || index >= slotGemIds.Count) return;
            var gemId = slotGemIds[index];
            if (!gemId.HasValue) return;

            if (currentMode == GemMode.Fusion)
            {
                SelectFusionGem(gemId.Value);
            }
            else
            {
                SelectConvertGem(gemId.Value);
            }

            UpdateSelectionUI();
        }

        private void SelectFusionGem(int gemId)
        {
            if (selectedBaseGemId == gemId || selectedMaterialGemId == gemId) return;

            var gem = GetGem(gemId);
            if (gem == null) return;

            if (!selectedBaseGemId.HasValue)
            {
                selectedBaseGemId = gemId;
                return;
            }

            var baseGem = GetGem(selectedBaseGemId.Value);
            if (baseGem == null)
            {
                selectedBaseGemId = gemId;
                return;
            }

            if (gem.Tier != baseGem.Tier)
            {
                return;
            }

            selectedMaterialGemId = gemId;
        }

        private void SelectConvertGem(int gemId)
        {
            selectedConvertGemId = gemId;
            selectedConvertTarget = null;
        }

        private void ClearFusionBase()
        {
            selectedBaseGemId = null;
            selectedMaterialGemId = null;
            UpdateSelectionUI();
        }

        private void ClearFusionMaterial()
        {
            selectedMaterialGemId = null;
            UpdateSelectionUI();
        }

        private void ClearConvertBase()
        {
            selectedConvertGemId = null;
            selectedConvertTarget = null;
            UpdateSelectionUI();
        }

        private void ClearSelectionOnModeChange()
        {
            if (currentMode == GemMode.Fusion)
            {
                selectedConvertGemId = null;
                selectedConvertTarget = null;
            }
            else
            {
                selectedBaseGemId = null;
                selectedMaterialGemId = null;
            }
        }

        private void UpdateSelectionUI()
        {
            UpdateFusionSlots();
            UpdateConvertSlots();
            UpdateGridSelectionStates();
            UpdateConvertButtons();
        }

        private void UpdateFusionSlots()
        {
            var baseGem = selectedBaseGemId.HasValue ? GetGem(selectedBaseGemId.Value) : null;
            var materialGem = selectedMaterialGemId.HasValue ? GetGem(selectedMaterialGemId.Value) : null;

            UpdateFusionSlotView(fusionBaseSlot, "기준", baseGem);
            UpdateFusionSlotView(fusionMaterialSlot, "재료", materialGem);

            if (baseGem != null && baseGem.Tier != GemTier.Legend)
            {
                var nextTier = GetNextTier(baseGem.Tier);
                var previewName = GetGemDisplayName(baseGem.Type, nextTier);
                var previewTier = GetTierLabel(nextTier);
                fusionResultSlot?.SetGem("성공 결과", previewName, previewTier, null);
            }
            else if (baseGem != null)
            {
                fusionResultSlot?.SetEmpty("성공 결과", "최고 등급");
            }
            else
            {
                fusionResultSlot?.SetEmpty("성공 결과", "보석 선택");
            }

            if (fusionChanceText != null)
            {
                fusionChanceText.text = baseGem == null
                    ? "성공 확률: -"
                    : baseGem.Tier == GemTier.Legend
                        ? "성공 확률: -"
                        : $"성공 확률: {GetFusionChance(baseGem.Tier)}%";
            }

            if (fusionWarningText != null)
            {
                fusionWarningText.text = "실패 시 재료 소멸\n동일 등급만 합성 가능";
            }

            if (fusionButton != null)
            {
                bool canFuse = baseGem != null
                    && materialGem != null
                    && baseGem.Tier != GemTier.Legend
                    && materialGem.Tier == baseGem.Tier;
                fusionButton.interactable = canFuse;
            }
        }

        private void UpdateFusionSlotView(GemSlotView slot, string roleLabel, GemData gem)
        {
            if (slot == null) return;

            if (gem == null)
            {
                slot.SetEmpty(roleLabel, "보석 선택");
                return;
            }

            slot.SetGem(roleLabel, GetGemDisplayName(gem), GetTierLabel(gem.Tier), null);
        }

        private void UpdateConvertSlots()
        {
            var baseGem = selectedConvertGemId.HasValue ? GetGem(selectedConvertGemId.Value) : null;
            if (baseGem == null)
            {
                convertBaseSlot?.SetEmpty("현재 보석", "보석 선택");
                convertResultSlot?.SetEmpty("전환 결과", "보석 선택");
                if (convertInfoText != null) convertInfoText.text = "랜덤 전환: 현재 타입 제외";
                return;
            }

            convertBaseSlot?.SetGem("현재 보석", GetGemDisplayName(baseGem), GetTierLabel(baseGem.Tier), null);

            if (selectedConvertTarget.HasValue)
            {
                var targetType = selectedConvertTarget.Value;
                convertResultSlot?.SetGem("전환 결과", GetGemDisplayName(targetType, baseGem.Tier), GetTierLabel(baseGem.Tier), null);
                if (convertInfoText != null) convertInfoText.text = "확정 전환: 선택 타입으로 변환";
            }
            else
            {
                convertResultSlot?.SetGem("전환 결과", "랜덤 전환", GetTierLabel(baseGem.Tier), null);
                if (convertInfoText != null) convertInfoText.text = "랜덤 전환: 현재 타입 제외";
            }
        }

        private void UpdateGridSelectionStates()
        {
            for (int i = 0; i < gridItems.Count; i++)
            {
                if (i >= slotGemIds.Count) continue;
                var gemId = slotGemIds[i];
                var role = GemSelectionRole.None;
                if (gemId.HasValue)
                {
                    if (selectedBaseGemId.HasValue && gemId.Value == selectedBaseGemId.Value)
                    {
                        role = GemSelectionRole.Base;
                    }
                    else if (selectedMaterialGemId.HasValue && gemId.Value == selectedMaterialGemId.Value)
                    {
                        role = GemSelectionRole.Material;
                    }
                    else if (selectedConvertGemId.HasValue && gemId.Value == selectedConvertGemId.Value)
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
            UpdateButtonState(filterCritChanceButton, currentFilter == GemFilter.CritChance, filterSelectedColor, filterUnselectedColor);
            UpdateButtonState(filterCritDamageButton, currentFilter == GemFilter.CritDamage, filterSelectedColor, filterUnselectedColor);
        }

        private void UpdateModeButtons()
        {
            UpdateButtonState(fusionModeButton, currentMode == GemMode.Fusion, modeSelectedColor, modeUnselectedColor);
            UpdateButtonState(convertModeButton, currentMode == GemMode.Convert, modeSelectedColor, modeUnselectedColor);
        }

        private void UpdateConvertButtons()
        {
            var baseGem = selectedConvertGemId.HasValue ? GetGem(selectedConvertGemId.Value) : null;
            bool hasBase = baseGem != null;

            if (convertRandomButton != null) convertRandomButton.interactable = hasBase;
            if (convertFixedAttackSpeedButton != null)
                convertFixedAttackSpeedButton.interactable = hasBase && baseGem.Type != GemType.AttackSpeed;
            if (convertFixedCritChanceButton != null)
                convertFixedCritChanceButton.interactable = hasBase && baseGem.Type != GemType.CritChance;
            if (convertFixedCritDamageButton != null)
                convertFixedCritDamageButton.interactable = hasBase && baseGem.Type != GemType.CritDamage;
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
            // 합성 로직은 추후 서버 연동 시 처리
        }

        private void OnConvertRandomClicked()
        {
            selectedConvertTarget = null;
            UpdateConvertSlots();
            UpdateConvertButtons();
        }

        private void OnConvertFixedClicked(GemType targetType)
        {
            var baseGem = selectedConvertGemId.HasValue ? GetGem(selectedConvertGemId.Value) : null;
            if (baseGem == null) return;
            if (baseGem.Type == targetType) return;

            selectedConvertTarget = targetType;
            UpdateConvertSlots();
            UpdateConvertButtons();
        }

        private void OnExpandRowClicked()
        {
            if (currentCapacity >= maxCapacity) return;

            currentCapacity = Mathf.Min(currentCapacity + capacityStep, maxCapacity);
            RebuildGrid();
        }

        private void UpdateExpandButtonState()
        {
            if (expandRowButton != null)
            {
                expandRowButton.interactable = currentCapacity < maxCapacity;
            }

            if (expandCostText != null)
            {
                expandCostText.text = "확장 비용: TBD";
            }
        }

        private void UpdateCapacityText()
        {
            if (capacityText != null)
            {
                capacityText.text = $"{currentCapacity}/{maxCapacity}";
            }
        }

        private GemData GetGem(int gemId)
        {
            return gemById.TryGetValue(gemId, out var gem) ? gem : null;
        }

        private string GetGemDisplayName(GemData gem)
        {
            return GetGemDisplayName(gem.Type, gem.Tier);
        }

        private string GetGemDisplayName(GemType type, GemTier tier)
        {
            return $"{GetTypeLabel(type)} 보석";
        }

        private string GetTypeLabel(GemType type)
        {
            return type switch
            {
                GemType.AttackSpeed => "공격속도",
                GemType.CritChance => "크확",
                GemType.CritDamage => "크뎀",
                _ => "보석"
            };
        }

        private string GetTierLabel(GemTier tier)
        {
            return tier switch
            {
                GemTier.Common => "커먼",
                GemTier.Uncommon => "언커먼",
                GemTier.Rare => "레어",
                GemTier.Epic => "에픽",
                GemTier.Legend => "레전드",
                _ => "커먼"
            };
        }

        private GemTier GetNextTier(GemTier tier)
        {
            return tier switch
            {
                GemTier.Common => GemTier.Uncommon,
                GemTier.Uncommon => GemTier.Rare,
                GemTier.Rare => GemTier.Epic,
                GemTier.Epic => GemTier.Legend,
                _ => GemTier.Legend
            };
        }

        private int GetFusionChance(GemTier tier)
        {
            return tier switch
            {
                GemTier.Common => 100,
                GemTier.Uncommon => 70,
                GemTier.Rare => 50,
                GemTier.Epic => 30,
                _ => 0
            };
        }
    }
}
