using System;
using System.Collections.Generic;

namespace InfinitePickaxe.Client.Metadata
{
    public sealed class InfiniteMineMetaResolver
    {
        private readonly Dictionary<uint, InfiniteMineFloorMeta> floorsByNumber = new Dictionary<uint, InfiniteMineFloorMeta>();
        private readonly List<InfiniteMineFloorMeta> floors = new List<InfiniteMineFloorMeta>();
        private bool initialized;
        private bool warnedNoMeta;

        public InfiniteMineMetaResolver()
        {
            InitializeFromMeta();
        }

        public IReadOnlyList<InfiniteMineFloorMeta> Floors => floors;
        public string ResetTimeKst { get; private set; } = string.Empty;
        public uint TimeLimitSec { get; private set; } = 60;
        public uint MaxFloor { get; private set; } = 100;
        public uint AutoRewardDivisor { get; private set; } = 10;

        public bool TryGetFloor(uint floor, out InfiniteMineFloorMeta meta)
        {
            return floorsByNumber.TryGetValue(floor, out meta);
        }

        public void Reload()
        {
            initialized = false;
            warnedNoMeta = false;
            floorsByNumber.Clear();
            floors.Clear();
            ResetTimeKst = string.Empty;
            TimeLimitSec = 60;
            MaxFloor = 100;
            AutoRewardDivisor = 10;
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

            if (!MetaRepository.Data.TryGetValue("infinite_mine", out var obj) || obj is not Dictionary<string, object> dict)
            {
                if (!warnedNoMeta)
                {
                    warnedNoMeta = true;
                    UnityEngine.Debug.LogWarning("InfiniteMineMetaResolver: infinite_mine section missing in meta_bundle.json.");
                }
                return;
            }

            if (TryGetString(dict, out var resetTime, "reset_time_kst"))
            {
                ResetTimeKst = resetTime;
            }

            if (TryGetUInt(dict, out var timeLimit, "time_limit_sec"))
            {
                TimeLimitSec = timeLimit;
            }

            if (TryGetUInt(dict, out var maxFloor, "max_floor"))
            {
                MaxFloor = maxFloor;
            }

            if (TryGetUInt(dict, out var divisor, "auto_reward_divisor"))
            {
                AutoRewardDivisor = divisor;
            }

            if (dict.TryGetValue("floors", out var floorsObj) && floorsObj is List<object> floorList)
            {
                foreach (var entry in floorList)
                {
                    if (entry is not Dictionary<string, object> floorDict) continue;

                    if (!TryGetUInt(floorDict, out var floor, "floor")) continue;
                    if (!TryGetUInt(floorDict, out var mineralInfoId, "mineral_info_id")) continue;

                    var meta = new InfiniteMineFloorMeta
                    {
                        Floor = floor,
                        MineralInfoId = mineralInfoId,
                        Hp = TryGetULong(floorDict, out var hp, "hp") ? hp : 0,
                        RewardGold = TryGetULong(floorDict, out var rewardGold, "reward_gold") ? rewardGold : 0,
                        RewardCrystal = TryGetULong(floorDict, out var rewardCrystal, "reward_crystal") ? rewardCrystal : 0,
                        BiomeId = TryGetUInt(floorDict, out var biomeId, "biome_id") ? biomeId : 0
                    };

                    floorsByNumber[floor] = meta;
                    floors.Add(meta);
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
                case uint u:
                    value = u;
                    return true;
                case int i when i >= 0:
                    value = (ulong)i;
                    return true;
                case long l when l >= 0:
                    value = (ulong)l;
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
    }

    public sealed class InfiniteMineFloorMeta
    {
        public uint Floor { get; set; }
        public uint MineralInfoId { get; set; }
        public ulong Hp { get; set; }
        public ulong RewardGold { get; set; }
        public ulong RewardCrystal { get; set; }
        public uint BiomeId { get; set; }
    }
}
