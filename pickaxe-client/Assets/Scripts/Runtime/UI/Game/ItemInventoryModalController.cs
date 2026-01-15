using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Infinitepickaxe;
using InfinitePickaxe.Client.Core;
using InfinitePickaxe.Client.Metadata;
using InfinitePickaxe.Client.Net;

namespace InfinitePickaxe.Client.UI.Game
{
    public sealed class ItemInventoryModalController : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] private Button backgroundButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button expandButton;
        [SerializeField] private TextMeshProUGUI capacityText;

        [Header("Expand Confirm Modal")]
        [SerializeField] private GameObject expandConfirmModal;
        [SerializeField] private Button expandBackgroundButton;
        [SerializeField] private TextMeshProUGUI expandCostText;
        [SerializeField] private TextMeshProUGUI expandCurrentCrystalText;
        [SerializeField] private Button expandConfirmButton;
        [SerializeField] private Button expandCancelButton;

        [Header("Grid")]
        [SerializeField] private RectTransform gridContent;
        [SerializeField] private ItemSlotView itemSlotPrefab;

        [Header("Detail")]
        [SerializeField] private GameObject detailPanel;
        [SerializeField] private Image detailIconImage;
        [SerializeField] private TextMeshProUGUI detailNameText;
        [SerializeField] private TextMeshProUGUI detailDescText;

        [Header("Choice")]
        [SerializeField] private GameObject choicePanel;
        [SerializeField] private RectTransform choiceContent;
        [SerializeField] private ItemChoiceOptionView choiceOptionPrefab;

        [Header("Use Count")]
        [SerializeField] private Button minButton;
        [SerializeField] private Button minusButton;
        [SerializeField] private TMP_InputField countInput;
        [SerializeField] private Button plusButton;
        [SerializeField] private Button maxButton;

        [Header("Action")]
        [SerializeField] private Button useButton;
        [SerializeField] private Button cancelButton;

        private readonly List<ItemSlotView> slotViews = new List<ItemSlotView>();
        private readonly List<ItemChoiceOptionView> choiceViews = new List<ItemChoiceOptionView>();

        private MessageHandler messageHandler;
        private ItemStateCache itemCache;
        private ItemMetaResolver itemMetaResolver;
        private RarityMetaResolver rarityMetaResolver;
        private RewardPackageMetaResolver rewardPackageResolver;
        private CurrencyMetaResolver currencyMetaResolver;

        private ItemSlotData selectedItem;
        private bool hasSelection;
        private uint selectedChoiceEntryId;
        private uint currentUseCount;
        private uint currentMaxUseCount;
        private bool suppressCountInput;
        private bool listRequestInFlight;
        private bool expandRequestInFlight;
        private bool useRequestInFlight;
        private bool subscribed;

        private readonly Color defaultFrameColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        private readonly Color defaultTextColor = Color.white;

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            RequestInventory();
        }

        public void Hide()
        {
            CloseExpandConfirmModal();
            gameObject.SetActive(false);
        }

        private void Awake()
        {
            BindButtons();
        }

        private void OnEnable()
        {
            Subscribe();
            RefreshList();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (subscribed) return;
            itemCache = ItemStateCache.Instance;
            messageHandler ??= MessageHandler.Instance;

            if (itemCache != null)
            {
                itemCache.OnInventoryChanged += HandleInventoryChanged;
            }

            if (messageHandler != null)
            {
                messageHandler.OnItemInventoryResponse += HandleItemInventoryResponse;
                messageHandler.OnItemInventoryExpandResult += HandleItemInventoryExpandResult;
                messageHandler.OnUseItemResult += HandleUseItemResult;
                messageHandler.OnErrorNotification += HandleErrorNotification;
            }

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;

            if (itemCache != null)
            {
                itemCache.OnInventoryChanged -= HandleInventoryChanged;
            }

            if (messageHandler != null)
            {
                messageHandler.OnItemInventoryResponse -= HandleItemInventoryResponse;
                messageHandler.OnItemInventoryExpandResult -= HandleItemInventoryExpandResult;
                messageHandler.OnUseItemResult -= HandleUseItemResult;
                messageHandler.OnErrorNotification -= HandleErrorNotification;
            }

            subscribed = false;
        }

        private void HandleInventoryChanged()
        {
            RefreshList();
        }

        private void HandleItemInventoryResponse(ItemInventoryResponse response)
        {
            listRequestInFlight = false;
            RefreshList();
        }

        private void HandleItemInventoryExpandResult(ItemInventoryExpandResult result)
        {
            expandRequestInFlight = false;
            RefreshList();
        }

        private void HandleUseItemResult(UseItemResult result)
        {
            useRequestInFlight = false;
            RefreshList();
        }

        private void HandleErrorNotification(ErrorNotification error)
        {
            if (error == null) return;

            if (!listRequestInFlight && !expandRequestInFlight && !useRequestInFlight) return;

            listRequestInFlight = false;
            expandRequestInFlight = false;
            useRequestInFlight = false;
            RefreshList();
            UpdateUseButtonState();
        }

        private void RequestInventory()
        {
            if (listRequestInFlight) return;
            messageHandler ??= MessageHandler.Instance;
            if (messageHandler == null) return;

            listRequestInFlight = true;
            messageHandler.RequestItemInventory();
        }

        private void RequestExpand()
        {
            if (expandRequestInFlight) return;
            messageHandler ??= MessageHandler.Instance;
            if (messageHandler == null) return;

            expandRequestInFlight = true;
            messageHandler.RequestItemInventoryExpand();
        }

        private void RequestUseItem()
        {
            if (!hasSelection) return;
            if (currentUseCount == 0) return;
            if (useRequestInFlight) return;

            uint itemId = selectedItem.ItemId;
            if (itemId == 0) return;

            var meta = ResolveItemMeta(itemId);
            if (!IsUsable(meta)) return;
            bool requiresChoice = RequiresChoice(meta, itemId);
            if (requiresChoice && selectedChoiceEntryId == 0) return;

            string requestId = Guid.NewGuid().ToString();
            useRequestInFlight = true;
            messageHandler ??= MessageHandler.Instance;
            messageHandler?.RequestUseItem(itemId, currentUseCount, requestId, selectedChoiceEntryId);
            UpdateUseButtonState();
        }

        private void RefreshList()
        {
            EnsureMeta();

            var entries = BuildSortedEntries();
            uint capacity = ResolveCapacity(entries.Count);
            uint usedSlots = itemCache != null && itemCache.UsedSlots > 0 ? itemCache.UsedSlots : (uint)entries.Count;

            UpdateCapacityText(usedSlots, capacity);
            UpdateExpandButton(capacity);
            EnsureSlotPool((int)capacity);
            ApplySlots(entries, capacity);
            ResolveSelection(entries);
        }

        private void UpdateCapacityText(uint usedSlots, uint capacity)
        {
            if (capacityText == null) return;
            capacityText.text = $"{usedSlots:N0} / {capacity:N0}";
        }

        private void UpdateExpandButton(uint capacity)
        {
            if (expandButton == null) return;

            uint maxCapacity = itemMetaResolver != null ? itemMetaResolver.InventoryConfig.MaxCapacity : 0;
            bool canExpand = maxCapacity == 0 || capacity < maxCapacity;
            expandButton.interactable = canExpand && !expandRequestInFlight;
        }

        private uint ResolveCapacity(int entryCount)
        {
            uint capacity = itemCache != null ? itemCache.Capacity : 0;
            if (capacity == 0 && itemMetaResolver != null)
            {
                capacity = itemMetaResolver.InventoryConfig.BaseCapacity;
            }
            if (capacity == 0 && entryCount > 0)
            {
                capacity = (uint)entryCount;
            }
            return capacity;
        }

        private List<ItemSlotData> BuildSortedEntries()
        {
            var result = new List<ItemSlotData>();
            if (itemCache == null) return result;

            foreach (var pair in itemCache.Stacks)
            {
                if (pair.Key == 0 || pair.Value == 0) continue;
                result.Add(new ItemSlotData
                {
                    ItemId = pair.Key,
                    Count = pair.Value,
                    InstanceId = string.Empty,
                    AcquiredAtMs = 0
                });
            }

            foreach (var instance in itemCache.Instances)
            {
                if (instance == null || instance.ItemId == 0 || string.IsNullOrEmpty(instance.ItemInstanceId)) continue;
                result.Add(new ItemSlotData
                {
                    ItemId = instance.ItemId,
                    Count = 1,
                    InstanceId = instance.ItemInstanceId,
                    AcquiredAtMs = instance.AcquiredAtMs
                });
            }

            result.Sort((a, b) =>
            {
                int rarityA = ResolveRarityOrder(a.ItemId);
                int rarityB = ResolveRarityOrder(b.ItemId);
                if (rarityA != rarityB)
                {
                    return rarityB.CompareTo(rarityA);
                }

                int itemIdCompare = a.ItemId.CompareTo(b.ItemId);
                if (itemIdCompare != 0) return itemIdCompare;

                int acquiredCompare = a.AcquiredAtMs.CompareTo(b.AcquiredAtMs);
                if (acquiredCompare != 0) return acquiredCompare;

                return string.Compare(a.InstanceId, b.InstanceId, StringComparison.Ordinal);
            });

            return result;
        }

        private int ResolveRarityOrder(uint itemId)
        {
            var meta = ResolveItemMeta(itemId);
            if (meta == null) return 0;

            if (rarityMetaResolver != null && rarityMetaResolver.TryGetRarity(meta.RarityId, out var rarity))
            {
                return rarity.SortOrder;
            }

            return (int)meta.RarityId;
        }

        private void EnsureSlotPool(int capacity)
        {
            if (itemSlotPrefab == null || gridContent == null) return;

            while (slotViews.Count < capacity)
            {
                var instance = Instantiate(itemSlotPrefab, gridContent, false);
                slotViews.Add(instance);
            }
        }

        private void ApplySlots(List<ItemSlotData> entries, uint capacity)
        {
            for (int i = 0; i < slotViews.Count; i++)
            {
                var view = slotViews[i];
                if (view == null) continue;

                bool isActive = i < capacity;
                view.gameObject.SetActive(isActive);
                if (!isActive) continue;

                ItemSlotData data = i < entries.Count ? entries[i] : default;
                var icon = ResolveItemIcon(data.ItemId);
                ResolveRarityColors(data.ItemId, out var frameColor, out var textColor);
                bool selected = IsSelected(data);

                view.Bind(data, icon, frameColor, textColor, selected, HandleSlotClicked);
            }
        }

        private void ResolveSelection(List<ItemSlotData> entries)
        {
            if (!hasSelection)
            {
                UpdateDetailPanel(null);
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (IsSelected(entries[i]))
                {
                    selectedItem = entries[i];
                    UpdateDetailPanel(ResolveItemMeta(selectedItem.ItemId));
                    UpdateSelectionHighlights();
                    return;
                }
            }

            ClearSelection();
        }

        private void HandleSlotClicked(ItemSlotView view)
        {
            if (view == null || view.IsEmpty)
            {
                ClearSelection();
                return;
            }

            selectedItem = view.Data;
            hasSelection = true;
            selectedChoiceEntryId = 0;
            UpdateSelectionHighlights();
            UpdateDetailPanel(ResolveItemMeta(selectedItem.ItemId));
        }

        private void ClearSelection()
        {
            hasSelection = false;
            selectedChoiceEntryId = 0;
            UpdateSelectionHighlights();
            UpdateDetailPanel(null);
        }

        private void UpdateSelectionHighlights()
        {
            for (int i = 0; i < slotViews.Count; i++)
            {
                var view = slotViews[i];
                if (view == null || !view.gameObject.activeSelf) continue;
                view.SetSelected(IsSelected(view.Data));
            }
        }

        private bool IsSelected(ItemSlotData data)
        {
            if (!hasSelection) return false;
            if (selectedItem.IsInstance)
            {
                return data.IsInstance && string.Equals(data.InstanceId, selectedItem.InstanceId, StringComparison.Ordinal);
            }
            return !data.IsInstance && data.ItemId == selectedItem.ItemId;
        }

        private void UpdateDetailPanel(ItemInfoMeta meta)
        {
            if (detailPanel != null)
            {
                detailPanel.SetActive(hasSelection && selectedItem.ItemId != 0);
            }

            if (!hasSelection || selectedItem.ItemId == 0)
            {
                ClearChoiceList();
                return;
            }

            if (detailIconImage != null)
            {
                var sprite = ResolveItemIcon(selectedItem.ItemId);
                detailIconImage.sprite = sprite;
                detailIconImage.enabled = sprite != null;
            }

            if (detailNameText != null)
            {
                detailNameText.text = meta != null && !string.IsNullOrEmpty(meta.DisplayName)
                    ? meta.DisplayName
                    : $"ITEM {selectedItem.ItemId}";
            }

            if (detailDescText != null)
            {
                detailDescText.text = $"보유: {selectedItem.Count:N0}";
            }

            bool requiresChoice = RequiresChoice(meta, selectedItem.ItemId);
            if (choicePanel != null)
            {
                choicePanel.SetActive(requiresChoice);
            }

            if (requiresChoice)
            {
                BuildChoiceList(selectedItem.ItemId);
            }
            else
            {
                ClearChoiceList();
                selectedChoiceEntryId = 0;
            }

            UpdateUseCountRange(meta);
            UpdateUseButtonState();
        }

        private void UpdateUseCountRange(ItemInfoMeta meta)
        {
            if (!hasSelection || selectedItem.ItemId == 0)
            {
                currentUseCount = 0;
                currentMaxUseCount = 0;
                SetCountInput(0);
                return;
            }

            if (meta != null && string.IsNullOrEmpty(meta.UseActionType))
            {
                currentUseCount = 0;
                currentMaxUseCount = 0;
                SetCountInput(0);
                return;
            }

            bool stackable = meta == null || meta.Stackable;
            ulong maxRaw = selectedItem.Count;
            if (maxRaw > uint.MaxValue)
            {
                maxRaw = uint.MaxValue;
            }
            uint maxCount = stackable ? (uint)maxRaw : 1u;
            currentMaxUseCount = maxCount;

            uint nextCount = currentUseCount > 0 ? currentUseCount : 1u;
            if (nextCount > currentMaxUseCount)
            {
                nextCount = currentMaxUseCount;
            }

            SetCountInput(nextCount);
        }

        private void SetCountInput(uint count)
        {
            uint clamped = count > currentMaxUseCount ? currentMaxUseCount : count;
            currentUseCount = clamped;

            if (countInput != null)
            {
                suppressCountInput = true;
                countInput.text = currentUseCount.ToString();
                suppressCountInput = false;
            }
            UpdateUseButtonState();
        }

        private void UpdateUseButtonState()
        {
            if (useButton == null) return;

            var meta = hasSelection ? ResolveItemMeta(selectedItem.ItemId) : null;
            bool requiresChoice = RequiresChoice(meta, selectedItem.ItemId);
            bool canUse = hasSelection
                          && IsUsable(meta)
                          && currentUseCount > 0
                          && (!requiresChoice || selectedChoiceEntryId > 0)
                          && !useRequestInFlight;

            useButton.interactable = canUse;
        }

        private void BuildChoiceList(uint itemId)
        {
            ClearChoiceList();

            if (rewardPackageResolver == null) return;
            if (!rewardPackageResolver.TryGetPackage(itemId, out var package)) return;
            if (!string.Equals(package.Mode, "SELECT", StringComparison.OrdinalIgnoreCase)) return;
            if (!rewardPackageResolver.TryGetEntries(itemId, out var entries)) return;

            if (choiceOptionPrefab == null || choiceContent == null) return;

            foreach (var entry in entries)
            {
                if (entry == null || entry.EntryId == 0) continue;

                var option = Instantiate(choiceOptionPrefab, choiceContent, false);
                var icon = ResolveChoiceIcon(entry);
                ResolveChoiceColors(entry, out var frameColor, out var textColor);
                bool selected = selectedChoiceEntryId == entry.EntryId;

                option.Bind(entry.EntryId, icon, entry.Amount, frameColor, textColor, selected, HandleChoiceClicked);
                choiceViews.Add(option);
            }
        }

        private void ClearChoiceList()
        {
            for (int i = 0; i < choiceViews.Count; i++)
            {
                if (choiceViews[i] != null)
                {
                    Destroy(choiceViews[i].gameObject);
                }
            }
            choiceViews.Clear();
        }

        private void HandleChoiceClicked(uint entryId)
        {
            if (entryId == 0) return;
            selectedChoiceEntryId = entryId;

            for (int i = 0; i < choiceViews.Count; i++)
            {
                var view = choiceViews[i];
                if (view == null) continue;
                view.SetSelected(entryId == selectedChoiceEntryId);
            }

            UpdateUseButtonState();
        }

        private Sprite ResolveChoiceIcon(RewardPackageEntryMeta entry)
        {
            if (entry == null) return null;

            switch (entry.RewardType?.ToLowerInvariant())
            {
                case "gold":
                    return ResolveCurrencySprite("GOLD");
                case "crystal":
                    return ResolveCurrencySprite("CRYSTAL");
                case "item":
                    return ResolveItemIcon(entry.RewardRefId);
                case "gem":
                    return GemSpriteLoader.GetGemSprite(entry.RewardRefId);
            }

            return null;
        }

        private void ResolveChoiceColors(RewardPackageEntryMeta entry, out Color frameColor, out Color textColor)
        {
            frameColor = defaultFrameColor;
            textColor = defaultTextColor;
            if (entry == null) return;

            uint rarityId = 0;
            switch (entry.RewardType?.ToLowerInvariant())
            {
                case "gold":
                    rarityId = ResolveCurrencyRarityId("GOLD");
                    break;
                case "crystal":
                    rarityId = ResolveCurrencyRarityId("CRYSTAL");
                    break;
                case "item":
                    var itemMeta = ResolveItemMeta(entry.RewardRefId);
                    if (itemMeta != null) rarityId = itemMeta.RarityId;
                    break;
                case "gem":
                    rarityId = ResolveGemRarityId(entry.RewardRefId);
                    break;
            }

            if (rarityId == 0) return;
            if (rarityMetaResolver != null && rarityMetaResolver.TryGetRarity(rarityId, out var rarity))
            {
                frameColor = rarity.BgColor;
                textColor = rarity.TextColor;
            }
        }

        private Sprite ResolveItemIcon(uint itemId)
        {
            var meta = ResolveItemMeta(itemId);
            if (meta == null) return null;
            return ItemSpriteLoader.GetItemSprite(meta.SpriteKey);
        }

        private Sprite ResolveCurrencySprite(string currencyType)
        {
            if (currencyMetaResolver != null && currencyMetaResolver.TryGetCurrencyByType(currencyType, out var meta))
            {
                return ItemSpriteLoader.GetCurrencySprite(meta.SpriteKey);
            }
            return null;
        }

        private uint ResolveCurrencyRarityId(string currencyType)
        {
            if (currencyMetaResolver != null && currencyMetaResolver.TryGetCurrencyByType(currencyType, out var meta))
            {
                return meta.RarityId;
            }
            return 0;
        }

        private uint ResolveGemRarityId(uint gemId)
        {
            if (gemId >= 400) return 5;
            if (gemId >= 300) return 4;
            if (gemId >= 200) return 3;
            if (gemId >= 100) return 2;
            return 1;
        }

        private void ResolveRarityColors(uint itemId, out Color frameColor, out Color textColor)
        {
            frameColor = defaultFrameColor;
            textColor = defaultTextColor;

            var meta = ResolveItemMeta(itemId);
            if (meta == null || meta.RarityId == 0) return;
            if (rarityMetaResolver != null && rarityMetaResolver.TryGetRarity(meta.RarityId, out var rarity))
            {
                frameColor = rarity.BgColor;
                textColor = rarity.TextColor;
            }
        }

        private ItemInfoMeta ResolveItemMeta(uint itemId)
        {
            if (itemId == 0 || itemMetaResolver == null) return null;
            itemMetaResolver.TryGetItem(itemId, out var meta);
            return meta;
        }

        private bool RequiresChoice(ItemInfoMeta meta, uint itemId)
        {
            if (meta != null && string.Equals(meta.UseActionType, "GEM_SELECT", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (rewardPackageResolver != null && rewardPackageResolver.TryGetPackage(itemId, out var package))
            {
                return string.Equals(package.Mode, "SELECT", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private bool IsUsable(ItemInfoMeta meta)
        {
            return meta == null || !string.IsNullOrEmpty(meta.UseActionType);
        }

        private void BindButtons()
        {
            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveAllListeners();
                backgroundButton.onClick.AddListener(Hide);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Hide);
            }

            if (expandButton != null)
            {
                expandButton.onClick.RemoveAllListeners();
                expandButton.onClick.AddListener(OpenExpandConfirmModal);
            }

            if (expandBackgroundButton != null)
            {
                expandBackgroundButton.onClick.RemoveAllListeners();
                expandBackgroundButton.onClick.AddListener(CloseExpandConfirmModal);
            }

            if (expandCancelButton != null)
            {
                expandCancelButton.onClick.RemoveAllListeners();
                expandCancelButton.onClick.AddListener(CloseExpandConfirmModal);
            }

            if (expandConfirmButton != null)
            {
                expandConfirmButton.onClick.RemoveAllListeners();
                expandConfirmButton.onClick.AddListener(OnConfirmExpand);
            }

            if (minButton != null)
            {
                minButton.onClick.RemoveAllListeners();
                minButton.onClick.AddListener(() => SetCountInput(0));
            }

            if (minusButton != null)
            {
                minusButton.onClick.RemoveAllListeners();
                minusButton.onClick.AddListener(() =>
                {
                    if (currentUseCount == 0) return;
                    SetCountInput(currentUseCount - 1);
                });
            }

            if (plusButton != null)
            {
                plusButton.onClick.RemoveAllListeners();
                plusButton.onClick.AddListener(() =>
                {
                    if (currentUseCount >= currentMaxUseCount) return;
                    SetCountInput(currentUseCount + 1);
                });
            }

            if (maxButton != null)
            {
                maxButton.onClick.RemoveAllListeners();
                maxButton.onClick.AddListener(() => SetCountInput(currentMaxUseCount));
            }

            if (useButton != null)
            {
                useButton.onClick.RemoveAllListeners();
                useButton.onClick.AddListener(RequestUseItem);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(ClearSelection);
            }

            if (countInput != null)
            {
                countInput.onValueChanged.RemoveAllListeners();
                countInput.onValueChanged.AddListener(HandleCountInputChanged);
                countInput.onEndEdit.RemoveAllListeners();
                countInput.onEndEdit.AddListener(HandleCountInputChanged);
            }
        }

        private void HandleCountInputChanged(string value)
        {
            if (suppressCountInput) return;

            if (string.IsNullOrEmpty(value))
            {
                SetCountInput(0);
                UpdateUseButtonState();
                return;
            }

            if (!uint.TryParse(value, out var parsed))
            {
                SetCountInput(0);
                UpdateUseButtonState();
                return;
            }

            SetCountInput(parsed);
            UpdateUseButtonState();
        }

        private void OpenExpandConfirmModal()
        {
            if (expandConfirmModal == null) return;
            EnsureMeta();

            uint currentCrystal = UserResourceCache.Instance.Crystal ?? 0;
            uint expandCost = itemMetaResolver != null ? itemMetaResolver.InventoryConfig.ExpandCost : 0;
            uint capacity = itemCache != null ? itemCache.Capacity : 0;
            if (capacity == 0 && itemMetaResolver != null)
            {
                capacity = itemMetaResolver.InventoryConfig.BaseCapacity;
            }

            if (expandCostText != null)
            {
                expandCostText.text = expandCost > 0
                    ? $"필요 크리스탈: {expandCost}"
                    : "필요 크리스탈: -";
            }

            if (expandCurrentCrystalText != null)
            {
                expandCurrentCrystalText.text = $"보유: {currentCrystal}";
            }

            if (expandConfirmButton != null)
            {
                expandConfirmButton.interactable = expandCost > 0
                                                   && currentCrystal >= expandCost
                                                   && IsInventoryExpandable(capacity)
                                                   && !expandRequestInFlight;
            }

            expandConfirmModal.SetActive(true);
            expandConfirmModal.transform.SetAsLastSibling();
        }

        private void CloseExpandConfirmModal()
        {
            if (expandConfirmModal != null)
            {
                expandConfirmModal.SetActive(false);
            }
        }

        private void OnConfirmExpand()
        {
            RequestExpand();
            CloseExpandConfirmModal();
        }

        private bool IsInventoryExpandable(uint capacity)
        {
            uint maxCapacity = itemMetaResolver != null ? itemMetaResolver.InventoryConfig.MaxCapacity : 0;
            return maxCapacity == 0 || capacity < maxCapacity;
        }

        private void EnsureMeta()
        {
            if (itemMetaResolver == null)
            {
                itemMetaResolver = new ItemMetaResolver();
            }
            else if (MetaRepository.Loaded && !itemMetaResolver.HasData)
            {
                itemMetaResolver.Reload();
            }

            if (rarityMetaResolver == null)
            {
                rarityMetaResolver = new RarityMetaResolver();
            }
            else if (MetaRepository.Loaded && !rarityMetaResolver.HasData)
            {
                rarityMetaResolver.Reload();
            }

            if (rewardPackageResolver == null)
            {
                rewardPackageResolver = new RewardPackageMetaResolver();
            }
            else if (MetaRepository.Loaded && !rewardPackageResolver.HasData)
            {
                rewardPackageResolver.Reload();
            }

            if (currencyMetaResolver == null)
            {
                currencyMetaResolver = new CurrencyMetaResolver();
            }
            else if (MetaRepository.Loaded && !currencyMetaResolver.HasData)
            {
                currencyMetaResolver.Reload();
            }

        }
    }
}
