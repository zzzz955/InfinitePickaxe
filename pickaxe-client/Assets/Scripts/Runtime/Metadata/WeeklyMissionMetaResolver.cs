using System;
using System.Collections.Generic;

namespace InfinitePickaxe.Client.Metadata
{
    public sealed class WeeklyMissionMetaResolver
    {
        private readonly Dictionary<uint, WeeklyMissionMeta> missionsById = new Dictionary<uint, WeeklyMissionMeta>();
        private readonly List<WeeklyMissionMeta> missions = new List<WeeklyMissionMeta>();
        private readonly List<WeeklyMissionMilestoneMeta> milestones = new List<WeeklyMissionMilestoneMeta>();
        private bool initialized;
        private bool warnedNoMeta;

        public WeeklyMissionMetaResolver()
        {
            InitializeFromMeta();
        }

        public IReadOnlyList<WeeklyMissionMeta> Missions => missions;
        public IReadOnlyList<WeeklyMissionMilestoneMeta> Milestones => milestones;
        public string ResetWeekdayKst { get; private set; } = string.Empty;
        public string ResetTimeKst { get; private set; } = string.Empty;

        public bool HasData => missions.Count > 0 || milestones.Count > 0;

        public bool TryGetMission(uint id, out WeeklyMissionMeta meta)
        {
            return missionsById.TryGetValue(id, out meta);
        }

        public void Reload()
        {
            initialized = false;
            warnedNoMeta = false;
            missionsById.Clear();
            missions.Clear();
            milestones.Clear();
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

            if (!MetaRepository.Data.TryGetValue("weekly_missions", out var obj) || obj is not Dictionary<string, object> dict)
            {
                if (!warnedNoMeta)
                {
                    warnedNoMeta = true;
                    UnityEngine.Debug.LogWarning("WeeklyMissionMetaResolver: weekly_missions section missing in meta_bundle.json.");
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

            if (dict.TryGetValue("missions", out var missionsObj) && missionsObj is List<object> missionList)
            {
                foreach (var entry in missionList)
                {
                    if (entry is not Dictionary<string, object> missionDict) continue;

                    if (!TryGetUInt(missionDict, out var id, "id"))
                    {
                        continue;
                    }

                    var meta = new WeeklyMissionMeta
                    {
                        Id = id,
                        Type = TryGetString(missionDict, out var type, "type") ? type : string.Empty,
                        Target = TryGetUInt(missionDict, out var target, "target") ? target : 0,
                        Title = TryGetString(missionDict, out var title, "title") ? title : string.Empty,
                        Description = TryGetString(missionDict, out var description, "description") ? description : string.Empty,
                        RewardCrystal = TryGetUInt(missionDict, out var rewardCrystal, "reward_crystal") ? rewardCrystal : 0
                    };

                    missionsById[id] = meta;
                    missions.Add(meta);
                }
            }

            if (dict.TryGetValue("milestone_rewards", out var milestoneObj) && milestoneObj is List<object> milestoneList)
            {
                foreach (var entry in milestoneList)
                {
                    if (entry is not Dictionary<string, object> milestoneDict) continue;

                    if (!TryGetUInt(milestoneDict, out var completed, "completed")) continue;

                    var reward = TryGetUInt(milestoneDict, out var rewardCrystal, "reward_crystal") ? rewardCrystal : 0;
                    milestones.Add(new WeeklyMissionMilestoneMeta
                    {
                        Completed = completed,
                        RewardCrystal = reward
                    });
                }

                milestones.Sort((a, b) => a.Completed.CompareTo(b.Completed));
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

        private static uint? TryGetNullableUInt(Dictionary<string, object> dict, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!dict.TryGetValue(key, out var obj)) continue;
                if (obj == null) return null;
                if (TryConvertToUInt(obj, out var parsed)) return parsed;
            }

            return null;
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

    public sealed class WeeklyMissionMeta
    {
        public uint Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public uint Target { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public uint RewardCrystal { get; set; }
    }

    public sealed class WeeklyMissionMilestoneMeta
    {
        public uint Completed { get; set; }
        public uint RewardCrystal { get; set; }
    }
}
