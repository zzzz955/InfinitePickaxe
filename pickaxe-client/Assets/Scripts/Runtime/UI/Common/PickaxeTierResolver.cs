using System;
using System.Collections.Generic;
using InfinitePickaxe.Client.Metadata;

namespace InfinitePickaxe.Client.UI.Common
{
    /// <summary>
    /// 메타데이터(pickaxe_upgrades/pickaxe_levels 등)에서 레벨별 곡괭이 티어를 조회하는 간단한 리졸버.
    /// 슬롯별 정의가 있으면 우선 사용하고, 없으면 공통 정의를 사용한다.
    /// </summary>
    public sealed class PickaxeTierResolver
    {
        private bool initialized;
        private bool warnedNoMeta;
        private readonly Dictionary<uint, uint> tierByLevel = new Dictionary<uint, uint>();
        private readonly Dictionary<uint, Dictionary<uint, uint>> tierBySlotAndLevel = new Dictionary<uint, Dictionary<uint, uint>>();

        public uint ResolveTier(uint slotIndex, uint level, uint fallbackTier = 1)
        {
            EnsureInitialized();
            if (level == 0) return fallbackTier;

            if (tierBySlotAndLevel.TryGetValue(slotIndex, out var byLevel) && byLevel != null && byLevel.TryGetValue(level, out var slotTier))
            {
                return slotTier;
            }

            if (tierByLevel.TryGetValue(level, out var tier))
            {
                return tier;
            }

            return fallbackTier;
        }

        private void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;

            if (!MetaRepository.Loaded || MetaRepository.Data == null)
            {
                return;
            }

            LoadFromMeta(MetaRepository.Data);
        }

        private void LoadFromMeta(IReadOnlyDictionary<string, object> data)
        {
            TryAddFromKey(data, "pickaxe_upgrades");
            TryAddFromNested(data, "pickaxe", "upgrades");
            TryAddFromNested(data, "pickaxe", "levels");
            TryAddFromKey(data, "pickaxe_levels");

            if (tierByLevel.Count == 0 && tierBySlotAndLevel.Count == 0 && !warnedNoMeta)
            {
                warnedNoMeta = true;
                UnityEngine.Debug.LogWarning("PickaxeTierResolver: 메타 데이터에서 곡괭이 티어 정보를 찾지 못했습니다.");
            }
        }

        private void TryAddFromKey(IReadOnlyDictionary<string, object> data, string key)
        {
            if (data.TryGetValue(key, out var obj))
            {
                AddRows(obj);
            }
        }

        private void TryAddFromNested(IReadOnlyDictionary<string, object> data, string parent, string child)
        {
            if (data.TryGetValue(parent, out var pickaxeObj) && pickaxeObj is Dictionary<string, object> pickaxeDict)
            {
                if (pickaxeDict.TryGetValue(child, out var childObj))
                {
                    AddRows(childObj);
                }
            }
        }

        private void AddRows(object obj)
        {
            if (obj is not List<object> list) return;

            foreach (var entry in list)
            {
                if (entry is not Dictionary<string, object> dict) continue;

                if (!TryGetUInt(dict, new[] { "level", "Level" }, out var level))
                    continue;

                var tier = ParseTier(dict);
                if (tier == 0) tier = 1;

                if (TryGetUInt(dict, new[] { "slot", "slot_index", "slotIndex" }, out var slotIndex))
                {
                    if (!tierBySlotAndLevel.TryGetValue(slotIndex, out var byLevel))
                    {
                        byLevel = new Dictionary<uint, uint>();
                        tierBySlotAndLevel[slotIndex] = byLevel;
                    }

                    byLevel[level] = tier;
                }
                else
                {
                    tierByLevel[level] = tier;
                }
            }
        }

        private static uint ParseTier(Dictionary<string, object> dict)
        {
            if (dict.TryGetValue("tier", out var obj))
            {
                switch (obj)
                {
                    case string s when s.StartsWith("T", StringComparison.OrdinalIgnoreCase) && uint.TryParse(s.Substring(1), out var parsed):
                        return parsed;
                    case string s when uint.TryParse(s, out var parsed2):
                        return parsed2;
                    case uint u:
                        return u;
                    case int i when i >= 0:
                        return (uint)i;
                }
            }

            return 0;
        }

        private static bool TryGetUInt(Dictionary<string, object> dict, IEnumerable<string> keys, out uint value)
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
                case int i when i >= 0:
                    value = (uint)i;
                    return true;
                case long l when l >= 0:
                    value = (uint)Math.Min(l, uint.MaxValue);
                    return true;
                case uint u:
                    value = u;
                    return true;
                case ulong ul:
                    value = (uint)Math.Min(ul, uint.MaxValue);
                    return true;
                case float f when f >= 0:
                    value = (uint)f;
                    return true;
                case double d when d >= 0:
                    value = (uint)d;
                    return true;
                case string s when uint.TryParse(s, out var parsed):
                    value = parsed;
                    return true;
                default:
                    value = 0;
                    return false;
            }
        }
    }
}
