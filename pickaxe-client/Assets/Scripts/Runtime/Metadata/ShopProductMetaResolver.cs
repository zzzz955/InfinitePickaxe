using System;
using System.Collections.Generic;

namespace InfinitePickaxe.Client.Metadata
{
    public sealed class ShopProductMetaResolver
    {
        private readonly Dictionary<uint, ShopProductMeta> productsById = new Dictionary<uint, ShopProductMeta>();
        private readonly List<ShopProductMeta> products = new List<ShopProductMeta>();
        private bool initialized;
        private bool warnedNoMeta;

        public ShopProductMetaResolver()
        {
            InitializeFromMeta();
        }

        public bool HasData => products.Count > 0;
        public IReadOnlyList<ShopProductMeta> Products => products;

        public bool TryGetProduct(uint productId, out ShopProductMeta meta)
        {
            return productsById.TryGetValue(productId, out meta);
        }

        public void Reload()
        {
            initialized = false;
            warnedNoMeta = false;
            productsById.Clear();
            products.Clear();
            InitializeFromMeta();
        }

        private void InitializeFromMeta()
        {
            if (initialized) return;
            initialized = true;

            if (!MetaRepository.Loaded || MetaRepository.Data == null)
            {
                return;
            }

            LoadShopProducts();
        }

        private void LoadShopProducts()
        {
            if (!MetaRepository.Data.TryGetValue("shop_products", out var obj) || obj is not List<object> list)
            {
                if (!warnedNoMeta)
                {
                    warnedNoMeta = true;
                    UnityEngine.Debug.LogWarning("ShopProductMetaResolver: shop_products 메타가 없습니다.");
                }
                return;
            }

            foreach (var entry in list)
            {
                if (entry is not Dictionary<string, object> dict) continue;

                if (!TryGetUInt(dict, out var productId, "product_id")) continue;
                if (productId == 0) continue;

                var meta = new ShopProductMeta
                {
                    ProductId = productId,
                    TabKey = TryGetString(dict, out var tabKey, "tab_key") ? tabKey : string.Empty,
                    ItemId = TryGetUInt(dict, out var itemId, "item_id") ? itemId : 0,
                    ItemCount = TryGetUInt(dict, out var itemCount, "item_count") ? itemCount : 1,
                    PriceCurrency = TryGetString(dict, out var currency, "price_currency") ? currency : string.Empty,
                    IsActive = TryGetBool(dict, out var isActive, "is_active") ? isActive : true,
                    DisplaySpriteKey = TryGetString(dict, out var displaySprite, "display_sprite_key") ? displaySprite : string.Empty
                };

                if (TryGetULong(dict, out var priceAmount, "price_amount"))
                {
                    meta.PriceAmount = priceAmount;
                }

                if (TryGetInt(dict, out var sortOrder, "sort_order"))
                {
                    meta.SortOrder = sortOrder;
                }

                productsById[productId] = meta;
                products.Add(meta);
            }
        }

        private static bool TryGetString(Dictionary<string, object> dict, out string value, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (dict.TryGetValue(key, out var obj) && obj != null)
                {
                    value = obj.ToString();
                    return true;
                }
            }

            value = string.Empty;
            return false;
        }

        private static bool TryGetUInt(Dictionary<string, object> dict, out uint value, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (dict.TryGetValue(key, out var obj) && TryConvertToUInt(obj, out value))
                {
                    return true;
                }
            }

            value = 0;
            return false;
        }

        private static bool TryGetULong(Dictionary<string, object> dict, out ulong value, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (dict.TryGetValue(key, out var obj) && TryConvertToULong(obj, out value))
                {
                    return true;
                }
            }

            value = 0;
            return false;
        }

        private static bool TryGetInt(Dictionary<string, object> dict, out int value, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (dict.TryGetValue(key, out var obj) && TryConvertToInt(obj, out value))
                {
                    return true;
                }
            }

            value = 0;
            return false;
        }

        private static bool TryGetBool(Dictionary<string, object> dict, out bool value, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (dict.TryGetValue(key, out var obj) && TryConvertToBool(obj, out value))
                {
                    return true;
                }
            }

            value = false;
            return false;
        }

        private static bool TryConvertToUInt(object obj, out uint value)
        {
            switch (obj)
            {
                case uint u:
                    value = u;
                    return true;
                case int i when i >= 0:
                    value = (uint)i;
                    return true;
                case long l when l >= 0:
                    value = (uint)Math.Min(l, uint.MaxValue);
                    return true;
                case ulong ul:
                    value = (uint)Math.Min(ul, uint.MaxValue);
                    return true;
                case double d when d >= 0:
                    value = (uint)d;
                    return true;
                case float f when f >= 0:
                    value = (uint)f;
                    return true;
                case string s when uint.TryParse(s, out var parsed):
                    value = parsed;
                    return true;
            }

            value = 0;
            return false;
        }

        private static bool TryConvertToULong(object obj, out ulong value)
        {
            switch (obj)
            {
                case ulong ul:
                    value = ul;
                    return true;
                case long l when l >= 0:
                    value = (ulong)l;
                    return true;
                case int i when i >= 0:
                    value = (ulong)i;
                    return true;
                case uint u:
                    value = u;
                    return true;
                case double d when d >= 0:
                    value = (ulong)d;
                    return true;
                case float f when f >= 0:
                    value = (ulong)f;
                    return true;
                case string s when ulong.TryParse(s, out var parsed):
                    value = parsed;
                    return true;
            }

            value = 0;
            return false;
        }

        private static bool TryConvertToInt(object obj, out int value)
        {
            switch (obj)
            {
                case int i:
                    value = i;
                    return true;
                case uint u:
                    value = (int)Math.Min(u, int.MaxValue);
                    return true;
                case long l:
                    value = (int)Math.Max(Math.Min(l, int.MaxValue), int.MinValue);
                    return true;
                case ulong ul:
                    value = (int)Math.Min(ul, (ulong)int.MaxValue);
                    return true;
                case double d:
                    value = (int)Math.Max(Math.Min(d, int.MaxValue), int.MinValue);
                    return true;
                case float f:
                    value = (int)Math.Max(Math.Min(f, int.MaxValue), int.MinValue);
                    return true;
                case string s when int.TryParse(s, out var parsed):
                    value = parsed;
                    return true;
            }

            value = 0;
            return false;
        }

        private static bool TryConvertToBool(object obj, out bool value)
        {
            switch (obj)
            {
                case bool b:
                    value = b;
                    return true;
                case int i:
                    value = i != 0;
                    return true;
                case long l:
                    value = l != 0;
                    return true;
                case uint u:
                    value = u != 0;
                    return true;
                case ulong ul:
                    value = ul != 0;
                    return true;
                case string s when bool.TryParse(s, out var parsed):
                    value = parsed;
                    return true;
                case string s when int.TryParse(s, out var parsedInt):
                    value = parsedInt != 0;
                    return true;
            }

            value = false;
            return false;
        }
    }

    public sealed class ShopProductMeta
    {
        public uint ProductId { get; set; }
        public string TabKey { get; set; } = string.Empty;
        public uint ItemId { get; set; }
        public uint ItemCount { get; set; }
        public string PriceCurrency { get; set; } = string.Empty;
        public ulong? PriceAmount { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public string DisplaySpriteKey { get; set; } = string.Empty;
    }
}
