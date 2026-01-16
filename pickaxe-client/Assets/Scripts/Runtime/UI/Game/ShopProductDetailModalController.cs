using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using InfinitePickaxe.Client.Core;
using InfinitePickaxe.Client.Metadata;
using InfinitePickaxe.Client.Net;
using Infinitepickaxe;

namespace InfinitePickaxe.Client.UI.Game
{
    public sealed class ShopProductDetailModalController : MonoBehaviour
    {
        [Header("Modal")]
        [SerializeField] private Button backgroundButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button purchaseButton;

        [Header("Info")]
        [SerializeField] private TextMeshProUGUI productNameText;
        [SerializeField] private TextMeshProUGUI productDescriptionText;
        [SerializeField] private Image rarityFrameImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private Color insufficientPriceColor = new Color(0.9f, 0.2f, 0.2f, 1f);

        [Header("Rewards")]
        [SerializeField] private RectTransform rewardContent;
        [SerializeField] private ItemChoiceOptionView rewardItemPrefab;

        [Header("Count")]
        [SerializeField] private Button minButton;
        [SerializeField] private Button minusButton;
        [SerializeField] private TMP_InputField countInput;
        [SerializeField] private Button plusButton;
        [SerializeField] private Button maxButton;

        private readonly List<ItemChoiceOptionView> rewardViews = new List<ItemChoiceOptionView>();
        private readonly Color defaultFrameColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        private readonly Color defaultTextColor = Color.white;

        private MessageHandler messageHandler;
        private UserResourceCache resourceCache;
        private ItemMetaResolver itemMetaResolver;
        private RewardPackageMetaResolver rewardPackageResolver;
        private RarityMetaResolver rarityMetaResolver;
        private CurrencyMetaResolver currencyMetaResolver;
        private GemMetaResolver gemMetaResolver;

        private ShopProductMeta currentProduct;
        private ItemInfoMeta currentItem;
        private ulong unitPrice;
        private bool hasPrice;
        private ulong currentCurrencyAmount;
        private uint maxAffordableCount;
        private uint currentCount;
        private bool suppressInput;
        private bool requestInFlight;
        private bool subscribed;
        private Color normalPriceColor;
        private bool hasPriceColor;

        public void Show(ShopProductMeta product)
        {
            if (product == null) return;
            EnsureMeta();

            currentProduct = product;
            currentItem = ResolveItemMeta(product.ItemId);
            requestInFlight = false;

            ApplyStaticInfo();
            BuildRewardList();
            UpdateCurrencyAmount();
            ClampCount(true);
            UpdatePriceText();
            UpdatePurchaseButton();

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void Awake()
        {
            BindButtons();
            if (priceText != null)
            {
                normalPriceColor = priceText.color;
                hasPriceColor = true;
            }
        }

        private void OnEnable()
        {
            Subscribe();
            UpdateCurrencyAmount();
            ClampCount(false);
            UpdatePriceText();
            UpdatePurchaseButton();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (subscribed) return;

            messageHandler ??= MessageHandler.Instance;
            resourceCache ??= UserResourceCache.Instance;

            if (messageHandler != null)
            {
                messageHandler.OnShopPurchaseResult += HandleShopPurchaseResult;
                messageHandler.OnErrorNotification += HandleErrorNotification;
            }

            if (resourceCache != null)
            {
                resourceCache.OnChanged += HandleResourceChanged;
            }

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;

            if (messageHandler != null)
            {
                messageHandler.OnShopPurchaseResult -= HandleShopPurchaseResult;
                messageHandler.OnErrorNotification -= HandleErrorNotification;
            }

            if (resourceCache != null)
            {
                resourceCache.OnChanged -= HandleResourceChanged;
            }

            subscribed = false;
        }

        private void HandleResourceChanged()
        {
            UpdateCurrencyAmount();
            ClampCount(false);
            UpdatePriceText();
            UpdatePurchaseButton();
        }

        private void HandleShopPurchaseResult(ShopPurchaseResult result)
        {
            if (result == null) return;
            if (currentProduct == null) return;
            if (result.ProductId != currentProduct.ProductId) return;

            requestInFlight = false;
            UpdatePurchaseButton();

            if (result.Success)
            {
                Hide();
                return;
            }

            UpdateCurrencyAmount();
            ClampCount(false);
            UpdatePriceText();
        }

        private void HandleErrorNotification(ErrorNotification error)
        {
            if (error == null) return;
            if (!requestInFlight) return;

            requestInFlight = false;
            UpdatePurchaseButton();
        }

        private void ApplyStaticInfo()
        {
            if (productNameText != null)
            {
                productNameText.text = currentItem != null && !string.IsNullOrEmpty(currentItem.DisplayName)
                    ? currentItem.DisplayName
                    : $"PRODUCT {currentProduct.ProductId}";
            }

            if (productDescriptionText != null)
            {
                string description = currentItem != null ? currentItem.Description : string.Empty;
                if (string.IsNullOrEmpty(description) && rewardPackageResolver != null
                    && rewardPackageResolver.TryGetPackage(currentProduct.ItemId, out var package))
                {
                    description = package.Description;
                }
                productDescriptionText.text = description ?? string.Empty;
            }

            if (rarityFrameImage != null)
            {
                rarityFrameImage.color = ResolveRarityFrameColor(currentItem);
            }

            if (iconImage != null)
            {
                var icon = ResolveProductIcon(currentProduct, currentItem);
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }

            if (currentProduct != null)
            {
                hasPrice = currentProduct.PriceAmount.HasValue;
                unitPrice = currentProduct.PriceAmount.GetValueOrDefault();
            }
            else
            {
                hasPrice = false;
                unitPrice = 0;
            }
        }

        private void BuildRewardList()
        {
            ClearRewardList();

            if (rewardPackageResolver == null || rewardContent == null || rewardItemPrefab == null) return;
            if (currentProduct == null) return;

            if (!rewardPackageResolver.TryGetEntries(currentProduct.ItemId, out var entries)) return;

            var sorted = new List<RewardPackageEntryMeta>(entries);
            sorted.Sort((a, b) => a.EntryId.CompareTo(b.EntryId));

            foreach (var entry in sorted)
            {
                if (entry == null || entry.EntryId == 0) continue;

                var view = Instantiate(rewardItemPrefab, rewardContent, false);
                var icon = ResolveRewardIcon(entry);
                ResolveRewardColors(entry, out var frameColor, out var textColor);
                view.Bind(entry.EntryId, icon, entry.Amount, frameColor, textColor, false, null);
                rewardViews.Add(view);
            }
        }

        private void ClearRewardList()
        {
            for (int i = 0; i < rewardViews.Count; i++)
            {
                if (rewardViews[i] != null)
                {
                    Destroy(rewardViews[i].gameObject);
                }
            }
            rewardViews.Clear();
        }

        private void UpdateCurrencyAmount()
        {
            currentCurrencyAmount = 0;
            if (currentProduct == null) return;

            string currency = currentProduct.PriceCurrency ?? string.Empty;
            if (string.Equals(currency, "CRYSTAL", StringComparison.OrdinalIgnoreCase))
            {
                if (resourceCache != null && resourceCache.Crystal.HasValue)
                {
                    currentCurrencyAmount = resourceCache.Crystal.Value;
                }
            }
            else if (string.Equals(currency, "GOLD", StringComparison.OrdinalIgnoreCase))
            {
                if (resourceCache != null && resourceCache.Gold.HasValue)
                {
                    currentCurrencyAmount = resourceCache.Gold.Value;
                }
            }
        }

        private uint ResolveAffordableMax()
        {
            if (!hasPrice) return 0;
            if (unitPrice == 0) return 1;
            if (currentCurrencyAmount == 0) return 0;

            ulong max = currentCurrencyAmount / unitPrice;
            if (max > uint.MaxValue) max = uint.MaxValue;
            return (uint)max;
        }

        private uint ResolveDisplayMax()
        {
            uint affordable = ResolveAffordableMax();
            if (!hasPrice) return 1;
            return affordable > 0 ? affordable : 1;
        }

        private void ClampCount(bool resetToMin)
        {
            maxAffordableCount = ResolveAffordableMax();
            uint displayMax = ResolveDisplayMax();

            uint next = currentCount;
            if (resetToMin || next == 0)
            {
                next = 1;
            }

            if (displayMax == 0)
            {
                next = 1;
            }
            else if (next > displayMax)
            {
                next = displayMax;
            }

            SetCountInput(next);
        }

        private void SetCountInput(uint count)
        {
            currentCount = count;
            if (countInput != null)
            {
                suppressInput = true;
                countInput.text = currentCount.ToString();
                suppressInput = false;
            }
        }

        private void UpdatePriceText()
        {
            if (priceText == null) return;

            if (!hasPrice)
            {
                priceText.text = "-";
                if (!hasPriceColor)
                {
                    normalPriceColor = priceText.color;
                    hasPriceColor = true;
                }
                priceText.color = normalPriceColor;
                return;
            }

            ulong total = 0;
            if (currentCount > 0)
            {
                if (unitPrice > 0 && unitPrice > ulong.MaxValue / currentCount)
                {
                    total = ulong.MaxValue;
                }
                else
                {
                    total = unitPrice * currentCount;
                }
            }

            priceText.text = total.ToString("N0");

            if (!hasPriceColor)
            {
                normalPriceColor = priceText.color;
                hasPriceColor = true;
            }

            bool affordable = unitPrice == 0 || (maxAffordableCount > 0 && currentCount <= maxAffordableCount);
            priceText.color = affordable ? normalPriceColor : insufficientPriceColor;
        }

        private void UpdatePurchaseButton()
        {
            if (purchaseButton == null) return;

            bool canBuy = hasPrice
                          && currentCount > 0
                          && !requestInFlight
                          && (unitPrice == 0 || (maxAffordableCount > 0 && currentCount <= maxAffordableCount));

            purchaseButton.interactable = canBuy;
        }

        private void RequestPurchase()
        {
            if (currentProduct == null) return;
            if (!hasPrice) return;
            if (currentCount == 0) return;
            if (requestInFlight) return;

            if (unitPrice > 0 && (maxAffordableCount == 0 || currentCount > maxAffordableCount))
            {
                UpdatePriceText();
                UpdatePurchaseButton();
                return;
            }

            requestInFlight = true;
            UpdatePurchaseButton();

            messageHandler ??= MessageHandler.Instance;
            if (messageHandler == null) return;

            string requestId = Guid.NewGuid().ToString();
            messageHandler.RequestShopPurchase(currentProduct.ProductId, currentCount, requestId);
        }

        private Sprite ResolveProductIcon(ShopProductMeta product, ItemInfoMeta itemMeta)
        {
            if (product != null && !string.IsNullOrEmpty(product.DisplaySpriteKey))
            {
                return ItemSpriteLoader.GetItemSprite(product.DisplaySpriteKey);
            }
            if (itemMeta != null)
            {
                return ItemSpriteLoader.GetItemSprite(itemMeta.SpriteKey);
            }
            return null;
        }

        private Color ResolveRarityFrameColor(ItemInfoMeta itemMeta)
        {
            if (itemMeta == null || rarityMetaResolver == null) return defaultFrameColor;
            if (itemMeta.RarityId == 0) return defaultFrameColor;
            if (rarityMetaResolver.TryGetRarity(itemMeta.RarityId, out var rarity))
            {
                return rarity.BgColor;
            }
            return defaultFrameColor;
        }

        private Sprite ResolveRewardIcon(RewardPackageEntryMeta entry)
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

        private void ResolveRewardColors(RewardPackageEntryMeta entry, out Color frameColor, out Color textColor)
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

            if (rarityId == 0 || rarityMetaResolver == null) return;
            if (rarityMetaResolver.TryGetRarity(rarityId, out var rarity))
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
            if (gemMetaResolver != null && gemMetaResolver.TryGetDefinition(gemId, out var definition))
            {
                return definition.GradeId;
            }
            return 0;
        }

        private ItemInfoMeta ResolveItemMeta(uint itemId)
        {
            if (itemId == 0 || itemMetaResolver == null) return null;
            itemMetaResolver.TryGetItem(itemId, out var meta);
            return meta;
        }

        private void BindButtons()
        {
            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveAllListeners();
                backgroundButton.onClick.AddListener(Hide);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(Hide);
            }

            if (purchaseButton != null)
            {
                purchaseButton.onClick.RemoveAllListeners();
                purchaseButton.onClick.AddListener(RequestPurchase);
            }

            if (minButton != null)
            {
                minButton.onClick.RemoveAllListeners();
                minButton.onClick.AddListener(() =>
                {
                    SetCountInput(1);
                    UpdatePriceText();
                    UpdatePurchaseButton();
                });
            }

            if (minusButton != null)
            {
                minusButton.onClick.RemoveAllListeners();
                minusButton.onClick.AddListener(() =>
                {
                    if (currentCount <= 1) return;
                    SetCountInput(currentCount - 1);
                    UpdatePriceText();
                    UpdatePurchaseButton();
                });
            }

            if (plusButton != null)
            {
                plusButton.onClick.RemoveAllListeners();
                plusButton.onClick.AddListener(() =>
                {
                    uint displayMax = ResolveDisplayMax();
                    if (currentCount >= displayMax) return;
                    SetCountInput(currentCount + 1);
                    UpdatePriceText();
                    UpdatePurchaseButton();
                });
            }

            if (maxButton != null)
            {
                maxButton.onClick.RemoveAllListeners();
                maxButton.onClick.AddListener(() =>
                {
                    SetCountInput(ResolveDisplayMax());
                    UpdatePriceText();
                    UpdatePurchaseButton();
                });
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
            if (suppressInput) return;

            if (!uint.TryParse(value, out var parsed))
            {
                parsed = 1;
            }

            uint displayMax = ResolveDisplayMax();
            if (parsed < 1) parsed = 1;
            if (parsed > displayMax) parsed = displayMax;

            SetCountInput(parsed);
            UpdatePriceText();
            UpdatePurchaseButton();
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

            if (rewardPackageResolver == null)
            {
                rewardPackageResolver = new RewardPackageMetaResolver();
            }
            else if (MetaRepository.Loaded && !rewardPackageResolver.HasData)
            {
                rewardPackageResolver.Reload();
            }

            if (rarityMetaResolver == null)
            {
                rarityMetaResolver = new RarityMetaResolver();
            }
            else if (MetaRepository.Loaded && !rarityMetaResolver.HasData)
            {
                rarityMetaResolver.Reload();
            }

            if (currencyMetaResolver == null)
            {
                currencyMetaResolver = new CurrencyMetaResolver();
            }
            else if (MetaRepository.Loaded && !currencyMetaResolver.HasData)
            {
                currencyMetaResolver.Reload();
            }

            if (gemMetaResolver == null)
            {
                gemMetaResolver = new GemMetaResolver();
            }
        }
    }
}
