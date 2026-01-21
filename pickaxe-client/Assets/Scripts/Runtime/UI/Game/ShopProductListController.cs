using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using InfinitePickaxe.Client.Core;
using InfinitePickaxe.Client.Metadata;

namespace InfinitePickaxe.Client.UI.Game
{
    public sealed class ShopProductListController : MonoBehaviour, IShopTabContent
    {
        [Header("Filter")]
        [SerializeField] private string tabKey = "GEM";

        [Header("Grid")]
        [SerializeField] private RectTransform gridContent;
        [SerializeField] private ShopProductCardView cardPrefab;
        [SerializeField] private TextMeshProUGUI emptyText;

        [Header("Detail Modal")]
        [SerializeField] private ShopProductDetailModalController detailModal;

        private readonly List<ShopProductCardView> cardPool = new List<ShopProductCardView>();
        private readonly Dictionary<uint, ShopProductMeta> productLookup = new Dictionary<uint, ShopProductMeta>();

        private ShopProductMetaResolver productMetaResolver;
        private ItemMetaResolver itemMetaResolver;
        private RarityMetaResolver rarityMetaResolver;
        private UserResourceCache resourceCache;
        private bool subscribed;

        private readonly Color defaultFrameColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);

        public void OnTabSelected()
        {
            Refresh();
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (subscribed) return;
            resourceCache ??= UserResourceCache.Instance;
            if (resourceCache != null)
            {
                resourceCache.OnChanged += HandleResourceChanged;
            }
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (resourceCache != null)
            {
                resourceCache.OnChanged -= HandleResourceChanged;
            }
            subscribed = false;
        }

        private void HandleResourceChanged()
        {
            Refresh();
        }

        public void Refresh()
        {
            EnsureMeta();
            resourceCache ??= UserResourceCache.Instance;
            var products = BuildProductList();

            EnsureCardPool(products.Count);
            productLookup.Clear();

            for (int i = 0; i < cardPool.Count; i++)
            {
                var view = cardPool[i];
                bool active = i < products.Count;
                if (view != null)
                {
                    view.gameObject.SetActive(active);
                }

                if (!active || view == null) continue;

                var product = products[i];
                productLookup[product.ProductId] = product;
                BindCard(view, product);
            }

            if (emptyText != null)
            {
                emptyText.gameObject.SetActive(products.Count == 0);
            }
        }

        private List<ShopProductMeta> BuildProductList()
        {
            var result = new List<ShopProductMeta>();
            if (productMetaResolver == null || !productMetaResolver.HasData) return result;

            string key = tabKey ?? string.Empty;
            foreach (var product in productMetaResolver.Products)
            {
                if (product == null || !product.IsActive) continue;
                if (!string.IsNullOrEmpty(key)
                    && !string.Equals(product.TabKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                result.Add(product);
            }

            result.Sort((a, b) =>
            {
                int order = a.SortOrder.CompareTo(b.SortOrder);
                if (order != 0) return order;
                return a.ProductId.CompareTo(b.ProductId);
            });

            return result;
        }

        private void EnsureCardPool(int count)
        {
            if (gridContent == null || cardPrefab == null) return;
            while (cardPool.Count < count)
            {
                var instance = Instantiate(cardPrefab, gridContent, false);
                cardPool.Add(instance);
            }
        }

        private void BindCard(ShopProductCardView view, ShopProductMeta product)
        {
            if (view == null || product == null) return;

            var itemMeta = ResolveItemMeta(product.ItemId);
            string title = itemMeta != null && !string.IsNullOrEmpty(itemMeta.DisplayName)
                ? itemMeta.DisplayName
                : $"PRODUCT {product.ProductId}";

            Sprite icon = ResolveProductIcon(product, itemMeta);
            Color frameColor = ResolveRarityFrameColor(itemMeta);

            bool isAffordable = IsAffordable(product);
            view.Bind(product.ProductId, title, icon, frameColor, product.PriceAmount, isAffordable, HandleCardClicked);
        }

        private void HandleCardClicked(uint productId)
        {
            if (detailModal == null) return;
            if (productLookup.TryGetValue(productId, out var product))
            {
                detailModal.Show(product);
            }
        }

        private bool IsAffordable(ShopProductMeta product)
        {
            if (product == null || !product.PriceAmount.HasValue) return true;
            if (product.PriceAmount.Value == 0) return true;

            if (string.Equals(product.PriceCurrency, "CRYSTAL", StringComparison.OrdinalIgnoreCase))
            {
                if (resourceCache == null || !resourceCache.Crystal.HasValue) return true;
                ulong crystal = resourceCache.Crystal.Value;
                return crystal >= product.PriceAmount.Value;
            }

            if (string.Equals(product.PriceCurrency, "GOLD", StringComparison.OrdinalIgnoreCase))
            {
                if (resourceCache == null || !resourceCache.Gold.HasValue) return true;
                ulong gold = resourceCache.Gold.Value;
                return gold >= product.PriceAmount.Value;
            }

            return true;
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

        private ItemInfoMeta ResolveItemMeta(uint itemId)
        {
            if (itemId == 0 || itemMetaResolver == null) return null;
            itemMetaResolver.TryGetItem(itemId, out var meta);
            return meta;
        }

        private void EnsureMeta()
        {
            if (productMetaResolver == null)
            {
                productMetaResolver = new ShopProductMetaResolver();
            }
            else if (MetaRepository.Loaded && !productMetaResolver.HasData)
            {
                productMetaResolver.Reload();
            }

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
        }
    }
}
