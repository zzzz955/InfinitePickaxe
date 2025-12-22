using System;
using System.Collections.Generic;
using Infinitepickaxe;

namespace InfinitePickaxe.Client.Core
{
    public sealed class QuestStateCache
    {
        private static readonly Lazy<QuestStateCache> Lazy = new Lazy<QuestStateCache>(() => new QuestStateCache());
        public static QuestStateCache Instance => Lazy.Value;

        private readonly List<MissionEntry> missions = new List<MissionEntry>();
        private readonly Dictionary<uint, int> missionIndexBySlot = new Dictionary<uint, int>();
        private readonly Dictionary<uint, int> missionIndexById = new Dictionary<uint, int>();

        private readonly HashSet<uint> claimedMilestones = new HashSet<uint>();
        private readonly Dictionary<string, AdCounter> adCountersByType = new Dictionary<string, AdCounter>(StringComparer.OrdinalIgnoreCase);

        public event Action OnMissionsChanged;
        public event Action OnMilestoneChanged;
        public event Action OnAdCountersChanged;

        public IReadOnlyList<MissionEntry> Missions => missions;
        public uint CompletedCount { get; private set; }
        public uint RerollCount { get; private set; }
        public uint RerollsFree { get; private set; }
        public uint RerollsTotalLimit { get; private set; }
        public ulong ResetTimestampMs { get; private set; }
        public bool HasDailyMissions { get; private set; }

        public uint MilestoneCompletedCount { get; private set; }
        public IReadOnlyCollection<uint> ClaimedMilestones => claimedMilestones;
        public ulong MilestoneResetTimestampMs { get; private set; }
        public bool HasMilestoneState { get; private set; }

        public IReadOnlyDictionary<string, AdCounter> AdCounters => adCountersByType;
        public ulong AdCountersResetTimestampMs { get; private set; }
        public bool HasAdCountersState { get; private set; }

        private QuestStateCache() { }

        public void ResetAll()
        {
            missions.Clear();
            missionIndexBySlot.Clear();
            missionIndexById.Clear();
            CompletedCount = 0;
            RerollCount = 0;
            RerollsFree = 0;
            RerollsTotalLimit = 0;
            ResetTimestampMs = 0;
            HasDailyMissions = false;

            claimedMilestones.Clear();
            MilestoneCompletedCount = 0;
            MilestoneResetTimestampMs = 0;
            HasMilestoneState = false;

            adCountersByType.Clear();
            AdCountersResetTimestampMs = 0;
            HasAdCountersState = false;

            OnMissionsChanged?.Invoke();
            OnMilestoneChanged?.Invoke();
            OnAdCountersChanged?.Invoke();
        }

        public void UpdateFromDailyMissionsResponse(DailyMissionsResponse response)
        {
            if (response == null) return;

            missions.Clear();
            missionIndexBySlot.Clear();
            missionIndexById.Clear();

            foreach (var mission in response.Missions)
            {
                if (mission == null) continue;
                int index = missions.Count;
                missions.Add(mission);

                if (mission.SlotNo > 0)
                {
                    missionIndexBySlot[mission.SlotNo] = index;
                }

                if (mission.MissionId > 0)
                {
                    missionIndexById[mission.MissionId] = index;
                }
            }

            CompletedCount = response.CompletedCount;
            RerollCount = response.RerollCount;
            RerollsFree = response.RerollsFree;
            RerollsTotalLimit = response.RerollsTotalLimit;
            ResetTimestampMs = response.ResetTimestampMs;
            HasDailyMissions = true;

            OnMissionsChanged?.Invoke();
        }

        public void UpdateFromMissionProgress(MissionProgressUpdate update)
        {
            if (update == null) return;

            if (!TryGetMissionIndex(update, out var index)) return;

            var mission = missions[index];
            mission.CurrentValue = update.CurrentValue;
            mission.TargetValue = update.TargetValue;
            mission.Status = update.Status ?? mission.Status;

            missions[index] = mission;
            OnMissionsChanged?.Invoke();
        }

        public void UpdateFromMilestoneState(MilestoneState state)
        {
            if (state == null) return;

            MilestoneCompletedCount = state.CompletedCount;
            claimedMilestones.Clear();
            foreach (var milestone in state.ClaimedMilestones)
            {
                claimedMilestones.Add(milestone);
            }
            MilestoneResetTimestampMs = state.ResetTimestampMs;
            HasMilestoneState = true;

            OnMilestoneChanged?.Invoke();
        }

        public void UpdateFromAdCountersState(AdCountersState state)
        {
            if (state == null) return;

            ReplaceAdCounters(state.AdCounters);
            AdCountersResetTimestampMs = state.ResetTimestampMs;
            HasAdCountersState = true;

            OnAdCountersChanged?.Invoke();
        }

        public void ApplyAdWatchResult(AdWatchResult result)
        {
            if (result == null || !result.Success) return;
            if (HasAdCountersState) return;

            ReplaceAdCounters(result.AdCounters);
            OnAdCountersChanged?.Invoke();
        }

        public bool TryGetAdCounter(string adType, out AdCounter counter)
        {
            if (string.IsNullOrWhiteSpace(adType))
            {
                counter = null;
                return false;
            }

            return adCountersByType.TryGetValue(adType, out counter);
        }

        public bool IsMilestoneClaimed(uint milestoneCount)
        {
            return claimedMilestones.Contains(milestoneCount);
        }

        private void ReplaceAdCounters(IEnumerable<AdCounter> counters)
        {
            adCountersByType.Clear();
            if (counters == null) return;

            foreach (var counter in counters)
            {
                if (counter == null || string.IsNullOrWhiteSpace(counter.AdType)) continue;
                adCountersByType[counter.AdType] = counter;
            }
        }

        private bool TryGetMissionIndex(MissionProgressUpdate update, out int index)
        {
            index = -1;

            if (update.SlotNo > 0 && missionIndexBySlot.TryGetValue(update.SlotNo, out index))
            {
                return index >= 0 && index < missions.Count;
            }

            if (update.MissionId > 0 && missionIndexById.TryGetValue(update.MissionId, out index))
            {
                return index >= 0 && index < missions.Count;
            }

            return false;
        }
    }
}
