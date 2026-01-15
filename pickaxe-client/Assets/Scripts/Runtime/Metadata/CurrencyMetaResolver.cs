using System;
using System.Collections.Generic;

namespace InfinitePickaxe.Client.Metadata
{
    public sealed class CurrencyMetaResolver
    {
        private readonly Dictionary<uint, CurrencyInfoMeta> currenciesById = new Dictionary<uint, CurrencyInfoMeta>();
        private readonly Dictionary<string, CurrencyInfoMeta> currenciesByType = new Dictionary<string, CurrencyInfoMeta>(StringComparer.OrdinalIgnoreCase);
        private bool initialized;
        private bool warnedNoMeta;

        public CurrencyMetaResolver()
        {
            InitializeFromMeta();
        }

        public bool HasData => currenciesById.Count > 0;

        public bool TryGetCurrency(uint currencyId, out CurrencyInfoMeta meta)
        {
            return currenciesById.TryGetValue(currencyId, out meta);
        }

        public bool TryGetCurrencyByType(string currencyType, out CurrencyInfoMeta meta)
        {
            if (string.IsNullOrEmpty(currencyType))
            {
                meta = null;
                return false;
            }

            return currenciesByType.TryGetValue(currencyType, out meta);
        }

        public void Reload()
        {
            initialized = false;
            warnedNoMeta = false;
            currenciesById.Clear();
            currenciesByType.Clear();
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

            LoadCurrencyInfo();
        }

        private void LoadCurrencyInfo()
        {
            if (!MetaRepository.Data.TryGetValue("currency_info", out var obj) || obj is not List<object> list)
            {
                if (!warnedNoMeta)
                {
                    warnedNoMeta = true;
                    UnityEngine.Debug.LogWarning("CurrencyMetaResolver: currency_info 메타가 없습니다.");
                }
                return;
            }

            foreach (var entry in list)
            {
                if (entry is not Dictionary<string, object> dict) continue;

                if (!TryGetUInt(dict, out var currencyId, "currency_id")) continue;
                if (currencyId == 0) continue;

                var meta = new CurrencyInfoMeta
                {
                    CurrencyId = currencyId,
                    CurrencyType = TryGetString(dict, out var type, "currency_type") ? type : string.Empty,
                    SpriteKey = TryGetString(dict, out var spriteKey, "sprite_key") ? spriteKey : string.Empty,
                    RarityId = TryGetUInt(dict, out var rarityId, "rarity_id") ? rarityId : 0,
                    DisplayName = TryGetString(dict, out var displayName, "display_name") ? displayName : string.Empty
                };

                currenciesById[currencyId] = meta;
                if (!string.IsNullOrEmpty(meta.CurrencyType))
                {
                    currenciesByType[meta.CurrencyType] = meta;
                }
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
    }

    public sealed class CurrencyInfoMeta
    {
        public uint CurrencyId { get; set; }
        public string CurrencyType { get; set; } = string.Empty;
        public string SpriteKey { get; set; } = string.Empty;
        public uint RarityId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }
}
