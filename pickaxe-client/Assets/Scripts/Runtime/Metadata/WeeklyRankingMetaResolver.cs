using System;
using System.Collections.Generic;

namespace InfinitePickaxe.Client.Metadata
{
    public sealed class WeeklyRankingMetaResolver
    {
        private readonly List<WeeklyRankingRewardMeta> rewards = new List<WeeklyRankingRewardMeta>();
        private bool initialized;
        private bool warnedNoMeta;

        public WeeklyRankingMetaResolver()
        {
            InitializeFromMeta();
        }

        public IReadOnlyList<WeeklyRankingRewardMeta> Rewards => rewards;
        public string ResetWeekdayKst { get; private set; } = string.Empty;
        public string ResetTimeKst { get; private set; } = string.Empty;
        public bool HasData => rewards.Count > 0;

        public void Reload()
        {
            initialized = false;
            warnedNoMeta = false;
            rewards.Clear();
            ResetWeekdayKst = string.Empty;
            ResetTimeKst = string.Empty;
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

            if (!MetaRepository.Data.TryGetValue("weekly_ranking", out var obj) || obj is not Dictionary<string, object> dict)
            {
                if (!warnedNoMeta)
                {
                    warnedNoMeta = true;
                    UnityEngine.Debug.LogWarning("WeeklyRankingMetaResolver: weekly_ranking section missing in meta_bundle.json.");
                }
                return;
            }

            if (TryGetString(dict, out var resetWeekday, "reset_weekday_kst"))
            {
                ResetWeekdayKst = resetWeekday;
            }

            if (TryGetString(dict, out var resetTime, "reset_time_kst"))
            {
                ResetTimeKst = resetTime;
            }

            if (dict.TryGetValue("rewards", out var rewardsObj) && rewardsObj is List<object> rewardList)
            {
                foreach (var entry in rewardList)
                {
                    if (entry is not Dictionary<string, object> rewardDict) continue;

                    if (!TryGetUInt(rewardDict, out var rankMin, "rank_min")) continue;
                    if (!TryGetUInt(rewardDict, out var rankMax, "rank_max")) continue;

                    var reward = new WeeklyRankingRewardMeta
                    {
                        RankMin = rankMin,
                        RankMax = rankMax,
                        RewardIndex = TryGetUInt(rewardDict, out var rewardIndex, "reward_index") ? rewardIndex : 0,
                        RewardType = TryGetString(rewardDict, out var rewardType, "reward_type") ? rewardType : string.Empty,
                        RewardKey = TryGetString(rewardDict, out var rewardKey, "reward_key") ? rewardKey : string.Empty,
                        Amount = TryGetULong(rewardDict, out var amount, "amount") ? amount : 0,
                        TemplateId = TryGetUInt(rewardDict, out var templateId, "template_id") ? templateId : 0
                    };

                    rewards.Add(reward);
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
    }

    public sealed class WeeklyRankingRewardMeta
    {
        public uint RankMin { get; set; }
        public uint RankMax { get; set; }
        public uint RewardIndex { get; set; }
        public string RewardType { get; set; } = string.Empty;
        public string RewardKey { get; set; } = string.Empty;
        public ulong Amount { get; set; }
        public uint TemplateId { get; set; }
    }
}
