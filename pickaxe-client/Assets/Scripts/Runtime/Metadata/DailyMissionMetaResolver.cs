using System;
using System.Collections.Generic;

namespace InfinitePickaxe.Client.Metadata
{
    public sealed class DailyMissionMetaResolver
    {
        private readonly Dictionary<uint, DailyMissionMeta> missionsById = new Dictionary<uint, DailyMissionMeta>();
        private readonly List<DailyMissionMeta> missions = new List<DailyMissionMeta>();
        private readonly List<DailyMissionMilestoneMeta> milestones = new List<DailyMissionMilestoneMeta>();
        private bool initialized;
        private bool warnedNoMeta;

        public DailyMissionMetaResolver()
        {
            InitializeFromMeta();
        }

        public IReadOnlyList<DailyMissionMeta> Missions => missions;
        public IReadOnlyList<DailyMissionMilestoneMeta> Milestones => milestones;
        public uint TotalSlots { get; private set; }
        public uint MaxDailyAssign { get; private set; }
        public string ResetTimeKst { get; private set; } = string.Empty;

        public bool HasData => missions.Count > 0 || milestones.Count > 0 || TotalSlots > 0 || MaxDailyAssign > 0;

        public bool TryGetMission(uint id, out DailyMissionMeta meta)
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
            TotalSlots = 0;
            MaxDailyAssign = 0;
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

            if (!MetaRepository.Data.TryGetValue("daily_missions", out var obj) || obj is not Dictionary<string, object> dict)
            {
                if (!warnedNoMeta)
                {
                    warnedNoMeta = true;
                    UnityEngine.Debug.LogWarning("DailyMissionMetaResolver: daily_missions section missing in meta_bundle.json.");
                }
                return;
            }

            if (TryGetString(dict, out var resetTime, "reset_time_kst"))
            {
                ResetTimeKst = resetTime;
            }

            if (TryGetUInt(dict, out var totalSlots, "total_slots"))
            {
                TotalSlots = totalSlots;
            }

            if (TryGetUInt(dict, out var maxDailyAssign, "max_daily_assign"))
            {
                MaxDailyAssign = maxDailyAssign;
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

                    var meta = new DailyMissionMeta
                    {
                        Id = id,
                        Difficulty = TryGetString(missionDict, out var difficulty, "difficulty") ? difficulty : string.Empty,
                        Type = TryGetString(missionDict, out var type, "type") ? type : string.Empty,
                        Target = TryGetUInt(missionDict, out var target, "target") ? target : 0,
                        MineralId = TryGetNullableUInt(missionDict, "mineral_id"),
                        Description = TryGetString(missionDict, out var description, "description") ? description : string.Empty,
                        RewardCrystal = TryGetUInt(missionDict, out var rewardCrystal, "reward_crystal") ? rewardCrystal : 0
                    };

                    missionsById[id] = meta;
                    missions.Add(meta);
                }
            }

            if (dict.TryGetValue("milestone_offline_bonus_hours", out var milestoneObj) && milestoneObj is List<object> milestoneList)
            {
                foreach (var entry in milestoneList)
                {
                    if (entry is not Dictionary<string, object> milestoneDict) continue;

                    if (!TryGetUInt(milestoneDict, out var completed, "completed")) continue;

                    var bonus = TryGetUInt(milestoneDict, out var bonusHours, "bonus_hours") ? bonusHours : 0;
                    milestones.Add(new DailyMissionMilestoneMeta
                    {
                        Completed = completed,
                        BonusHours = bonus
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

    public sealed class DailyMissionMeta
    {
        public uint Id { get; set; }
        public string Difficulty { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public uint Target { get; set; }
        public uint? MineralId { get; set; }
        public string Description { get; set; } = string.Empty;
        public uint RewardCrystal { get; set; }
    }

    public sealed class DailyMissionMilestoneMeta
    {
        public uint Completed { get; set; }
        public uint BonusHours { get; set; }
    }
}
