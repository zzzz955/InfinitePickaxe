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
            CritRate = 2,      // 프로토콜과 일치
            CritDmg = 3        // 프로토콜과 일치
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
        [SerializeField] private Button convertFixedCritRateButton;
        [SerializeField] private Button convertFixedCritDmgButton;

        [Header("스텁 데이터")]
        [SerializeField] private bool useStubData = true;
        [SerializeField] private int stubGemCount = 24;

        private readonly List<GemUIData> allGems = new List<GemUIData>();
        private readonly Dictionary<string, GemUIData> gemByInstanceId = new Dictionary<string, GemUIData>();
        private readonly List<GemGridItemView> gridItems = new List<GemGridItemView>();
        private readonly List<string> slotGemInstanceIds = new List<string>();
        private int currentCapacity;
        private GemFilter currentFilter = GemFilter.All;
        private GemMode currentMode = GemMode.Fusion;
        private string selectedBaseGemId;
        private string selectedMaterialGemId;
        private string selectedConvertGemId;
        private Infinitepickaxe.GemType? selectedConvertTarget;

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
            slotGemInstanceIds.Clear();

            var filtered = GetFilteredGems();
            for (int i = 0; i < currentCapacity; i++)
            {
                var view = gridItems[i];
                if (i < filtered.Count)
                {
                    var gem = filtered[i];
                    slotGemInstanceIds.Add(gem.GemInstanceId);
                    view.SetData(GetGemDisplayName(gem), GetGradeLabel(gem.Grade), null);
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
            else
            {
                SelectConvertGem(gemInstanceId);
            }

            UpdateSelectionUI();
        }

        private void SelectFusionGem(string gemInstanceId)
        {
            if (selectedBaseGemId == gemInstanceId || selectedMaterialGemId == gemInstanceId) return;

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
                return;
            }

            if (gem.Grade != baseGem.Grade)
            {
                return;
            }

            selectedMaterialGemId = gemInstanceId;
        }

        private void SelectConvertGem(string gemInstanceId)
        {
            selectedConvertGemId = gemInstanceId;
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
            var baseGem = !string.IsNullOrEmpty(selectedBaseGemId) ? GetGem(selectedBaseGemId) : null;
            var materialGem = !string.IsNullOrEmpty(selectedMaterialGemId) ? GetGem(selectedMaterialGemId) : null;

            UpdateFusionSlotView(fusionBaseSlot, "기준", baseGem);
            UpdateFusionSlotView(fusionMaterialSlot, "재료", materialGem);

            if (baseGem != null && baseGem.Grade != Infinitepickaxe.GemGrade.Legendary)
            {
                var nextGrade = GetNextGrade(baseGem.Grade);
                var previewName = GetGemDisplayName(baseGem.Type, nextGrade);
                var previewTier = GetGradeLabel(nextGrade);
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
                    : baseGem.Grade == Infinitepickaxe.GemGrade.Legendary
                        ? "성공 확률: -"
                        : $"성공 확률: {GetFusionChance(baseGem.Grade)}%";
            }

            if (fusionWarningText != null)
            {
                fusionWarningText.text = "실패 시 재료 소멸\n동일 등급만 합성 가능";
            }

            if (fusionButton != null)
            {
                bool canFuse = baseGem != null
                    && materialGem != null
                    && baseGem.Grade != Infinitepickaxe.GemGrade.Legendary
                    && materialGem.Grade == baseGem.Grade;
                fusionButton.interactable = canFuse;
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

            slot.SetGem(roleLabel, GetGemDisplayName(gem), GetGradeLabel(gem.Grade), null);
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

            convertBaseSlot?.SetGem("현재 보석", GetGemDisplayName(baseGem), GetGradeLabel(baseGem.Grade), null);

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
                    if (!string.IsNullOrEmpty(selectedBaseGemId) && gemInstanceId == selectedBaseGemId)
                    {
                        role = GemSelectionRole.Base;
                    }
                    else if (!string.IsNullOrEmpty(selectedMaterialGemId) && gemInstanceId == selectedMaterialGemId)
                    {
                        role = GemSelectionRole.Material;
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
            // TODO: 서버에 합성 요청 (GemSynthesisRequest)
            Debug.Log($"합성 요청: base={selectedBaseGemId}, material={selectedMaterialGemId}");
        }

        private void OnConvertRandomClicked()
        {
            selectedConvertTarget = null;
            UpdateConvertSlots();
            UpdateConvertButtons();
            // TODO: 서버에 랜덤 전환 요청 (GemConversionRequest with random=true)
            Debug.Log($"랜덤 전환 요청: gem={selectedConvertGemId}");
        }

        private void OnConvertFixedClicked(Infinitepickaxe.GemType targetType)
        {
            var baseGem = !string.IsNullOrEmpty(selectedConvertGemId) ? GetGem(selectedConvertGemId) : null;
            if (baseGem == null) return;
            if (baseGem.Type == targetType) return;

            selectedConvertTarget = targetType;
            UpdateConvertSlots();
            UpdateConvertButtons();
            // TODO: 서버에 확정 전환 요청 (GemConversionRequest with target_type)
            Debug.Log($"확정 전환 요청: gem={selectedConvertGemId}, target={targetType}");
        }

        private void OnExpandRowClicked()
        {
            if (currentCapacity >= maxCapacity) return;

            currentCapacity = Mathf.Min(currentCapacity + capacityStep, maxCapacity);
            RebuildGrid();
            // TODO: 서버에 인벤토리 확장 요청
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

        private GemUIData GetGem(string gemInstanceId)
        {
            return gemByInstanceId.TryGetValue(gemInstanceId, out var gem) ? gem : null;
        }

        private string GetGemDisplayName(GemUIData gem)
        {
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
            return grade switch
            {
                Infinitepickaxe.GemGrade.Common => Infinitepickaxe.GemGrade.Rare,
                Infinitepickaxe.GemGrade.Rare => Infinitepickaxe.GemGrade.Epic,
                Infinitepickaxe.GemGrade.Epic => Infinitepickaxe.GemGrade.Hero,
                Infinitepickaxe.GemGrade.Hero => Infinitepickaxe.GemGrade.Legendary,
                _ => Infinitepickaxe.GemGrade.Legendary
            };
        }

        private int GetFusionChance(Infinitepickaxe.GemGrade grade)
        {
            // TODO: 서버 메타데이터 또는 설정에서 가져오기
            return grade switch
            {
                Infinitepickaxe.GemGrade.Common => 100,
                Infinitepickaxe.GemGrade.Rare => 70,
                Infinitepickaxe.GemGrade.Epic => 50,
                Infinitepickaxe.GemGrade.Hero => 30,
                _ => 0
            };
        }
    }
}
