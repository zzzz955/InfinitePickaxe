using System;
using System.Collections.Generic;
using UnityEngine;

namespace InfinitePickaxe.Client.Metadata
{
    public sealed class RarityMetaResolver
    {
        private readonly Dictionary<uint, RarityMeta> rarityById = new Dictionary<uint, RarityMeta>();
        private bool initialized;
        private bool warnedNoMeta;

        public RarityMetaResolver()
        {
            InitializeFromMeta();
        }

        public bool HasData => rarityById.Count > 0;

        public bool TryGetRarity(uint rarityId, out RarityMeta meta)
        {
            return rarityById.TryGetValue(rarityId, out meta);
        }

        public void Reload()
        {
            initialized = false;
            warnedNoMeta = false;
            rarityById.Clear();
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

            LoadRarityInfo();
        }

        private void LoadRarityInfo()
        {
            if (!MetaRepository.Data.TryGetValue("rarity_info", out var obj) || obj is not List<object> list)
            {
                if (!warnedNoMeta)
                {
                    warnedNoMeta = true;
                    Debug.LogWarning("RarityMetaResolver: rarity_info 메타가 없습니다.");
                }
                return;
            }

            foreach (var entry in list)
            {
                if (entry is not Dictionary<string, object> dict) continue;

                if (!TryGetUInt(dict, out var rarityId, "rarity_id")) continue;
                if (rarityId == 0) continue;

                var meta = new RarityMeta
                {
                    RarityId = rarityId,
                    RarityName = TryGetString(dict, out var name, "rarity_name") ? name : string.Empty,
                    SortOrder = TryGetInt(dict, out var sortOrder, "sort_order") ? sortOrder : 0
                };

                if (TryGetString(dict, out var bgColor, "bg_color") && TryParseColor(bgColor, out var parsedBg))
                {
                    meta.BgColor = parsedBg;
                }

                if (TryGetString(dict, out var textColor, "text_color") && TryParseColor(textColor, out var parsedText))
                {
                    meta.TextColor = parsedText;
                }

                rarityById[rarityId] = meta;
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

        private static bool TryParseColor(string hex, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrEmpty(hex)) return false;

            var trimmed = hex.StartsWith("#", StringComparison.Ordinal) ? hex.Substring(1) : hex;
            if (trimmed.Length != 6 && trimmed.Length != 8) return false;

            if (!uint.TryParse(trimmed, System.Globalization.NumberStyles.HexNumber, null, out var raw))
            {
                return false;
            }

            byte r;
            byte g;
            byte b;
            byte a;

            if (trimmed.Length == 6)
            {
                r = (byte)((raw >> 16) & 0xFF);
                g = (byte)((raw >> 8) & 0xFF);
                b = (byte)(raw & 0xFF);
                a = 0xFF;
            }
            else
            {
                r = (byte)((raw >> 24) & 0xFF);
                g = (byte)((raw >> 16) & 0xFF);
                b = (byte)((raw >> 8) & 0xFF);
                a = (byte)(raw & 0xFF);
            }

            color = new Color32(r, g, b, a);
            return true;
        }
    }

    public sealed class RarityMeta
    {
        public uint RarityId { get; set; }
        public string RarityName { get; set; } = string.Empty;
        public Color BgColor { get; set; } = Color.white;
        public Color TextColor { get; set; } = Color.white;
        public int SortOrder { get; set; }
    }
}
