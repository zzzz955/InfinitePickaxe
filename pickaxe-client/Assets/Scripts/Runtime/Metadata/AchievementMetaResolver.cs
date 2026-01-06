using System;
using System.Collections.Generic;

namespace InfinitePickaxe.Client.Metadata
{
    public sealed class AchievementMetaResolver
    {
        private readonly Dictionary<uint, AchievementMeta> achievementsById = new Dictionary<uint, AchievementMeta>();
        private readonly Dictionary<uint, List<AchievementMeta>> achievementsByChain = new Dictionary<uint, List<AchievementMeta>>();
        private readonly List<AchievementMeta> achievements = new List<AchievementMeta>();
        private bool initialized;
        private bool warnedNoMeta;

        public AchievementMetaResolver()
        {
            InitializeFromMeta();
        }

        public IReadOnlyList<AchievementMeta> Achievements => achievements;
        public IReadOnlyDictionary<uint, List<AchievementMeta>> Chains => achievementsByChain;
        public bool HasData => achievements.Count > 0;

        public bool TryGetAchievement(uint id, out AchievementMeta meta)
        {
            return achievementsById.TryGetValue(id, out meta);
        }

        public bool TryGetChain(uint chainId, out List<AchievementMeta> steps)
        {
            return achievementsByChain.TryGetValue(chainId, out steps);
        }

        public void Reload()
        {
            initialized = false;
            warnedNoMeta = false;
            achievementsById.Clear();
            achievementsByChain.Clear();
            achievements.Clear();
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

            if (!MetaRepository.Data.TryGetValue("achievements", out var obj) || obj is not List<object> list)
            {
                if (!warnedNoMeta)
                {
                    warnedNoMeta = true;
                    UnityEngine.Debug.LogWarning("AchievementMetaResolver: achievements section missing in meta_bundle.json.");
                }
                return;
            }

            foreach (var entry in list)
            {
                if (entry is not Dictionary<string, object> dict) continue;

                if (!TryGetUInt(dict, out var id, "achievement_id", "id"))
                {
                    continue;
                }

                var meta = new AchievementMeta
                {
                    Id = id,
                    ChainId = TryGetUInt(dict, out var chainId, "chain_id") ? chainId : 0,
                    StepIndex = TryGetUInt(dict, out var stepIndex, "step_index") ? stepIndex : 0,
                    Type = TryGetString(dict, out var type, "type") ? type : string.Empty,
                    Target = TryGetULong(dict, out var target, "target") ? target : 0,
                    Title = TryGetString(dict, out var title, "title") ? title : string.Empty,
                    Description = TryGetString(dict, out var description, "description") ? description : string.Empty,
                    RewardCrystal = TryGetUInt(dict, out var rewardCrystal, "reward_crystal") ? rewardCrystal : 0,
                    RewardGold = TryGetULong(dict, out var rewardGold, "reward_gold") ? rewardGold : 0
                };

                achievementsById[id] = meta;
                achievements.Add(meta);

                if (!achievementsByChain.TryGetValue(meta.ChainId, out var chainList))
                {
                    chainList = new List<AchievementMeta>();
                    achievementsByChain[meta.ChainId] = chainList;
                }
                chainList.Add(meta);
            }

            foreach (var chain in achievementsByChain.Values)
            {
                chain.Sort((a, b) => a.StepIndex.CompareTo(b.StepIndex));
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
                case long l when l >= 0:
                    value = (ulong)l;
                    return true;
                case uint u:
                    value = u;
                    return true;
                case int i when i >= 0:
                    value = (ulong)i;
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

    public sealed class AchievementMeta
    {
        public uint Id { get; set; }
        public uint ChainId { get; set; }
        public uint StepIndex { get; set; }
        public string Type { get; set; } = string.Empty;
        public ulong Target { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public uint RewardCrystal { get; set; }
        public ulong RewardGold { get; set; }
    }
}
