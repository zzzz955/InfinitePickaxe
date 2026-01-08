using System;
using System.Collections.Generic;
using Infinitepickaxe;

namespace InfinitePickaxe.Client.Core
{
    public sealed class InfiniteMineStateCache
    {
        private static readonly Lazy<InfiniteMineStateCache> Lazy = new Lazy<InfiniteMineStateCache>(() => new InfiniteMineStateCache());
        public static InfiniteMineStateCache Instance => Lazy.Value;

        private readonly Dictionary<uint, InfiniteMineFloorState> floorStates = new Dictionary<uint, InfiniteMineFloorState>();

        public event Action OnStateChanged;

        public ulong ResetTimestampMs { get; private set; }
        public uint TimeLimitSec { get; private set; }
        public uint MaxFloor { get; private set; }
        public uint HighestClearedFloor { get; private set; }
        public bool HasState { get; private set; }

        public IReadOnlyDictionary<uint, InfiniteMineFloorState> FloorStates => floorStates;

        private InfiniteMineStateCache() { }

        public void Reset()
        {
            ResetTimestampMs = 0;
            TimeLimitSec = 0;
            MaxFloor = 0;
            HighestClearedFloor = 0;
            HasState = false;
            floorStates.Clear();
            OnStateChanged?.Invoke();
        }

        public void UpdateFromState(InfiniteMineStateResponse response)
        {
            if (response == null) return;

            ResetTimestampMs = response.ResetTimestampMs;
            TimeLimitSec = response.TimeLimitSec;
            MaxFloor = response.MaxFloor;
            HighestClearedFloor = response.HighestClearedFloor;
            HasState = true;

            floorStates.Clear();
            foreach (var state in response.FloorStates)
            {
                if (state == null || state.Floor == 0) continue;
                floorStates[state.Floor] = state;
            }

            OnStateChanged?.Invoke();
        }

        public bool TryGetFloorState(uint floor, out InfiniteMineFloorState state)
        {
            return floorStates.TryGetValue(floor, out state);
        }

        public bool IsAutoClaimable(uint floor)
        {
            return floorStates.TryGetValue(floor, out var state) && state.AutoClaimable;
        }

        public bool IsAutoClaimedToday(uint floor)
        {
            return floorStates.TryGetValue(floor, out var state) && state.AutoClaimedToday;
        }

        public void ApplyAutoClaimResult(InfiniteMineAutoClaimResult result)
        {
            if (result == null || !result.Success || result.Floor == 0) return;

            if (!floorStates.TryGetValue(result.Floor, out var state) || state == null)
            {
                state = new InfiniteMineFloorState { Floor = result.Floor };
            }

            state.AutoClaimable = false;
            state.AutoClaimedToday = true;
            floorStates[result.Floor] = state;

            OnStateChanged?.Invoke();
        }

        public void ApplyAutoClaimAllResult(InfiniteMineAutoClaimAllResult result)
        {
            if (result == null || !result.Success) return;

            if (floorStates.Count == 0) return;

            var keys = new List<uint>(floorStates.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                var floor = keys[i];
                if (!floorStates.TryGetValue(floor, out var state) || state == null) continue;
                if (!state.AutoClaimable) continue;

                state.AutoClaimable = false;
                state.AutoClaimedToday = true;
                floorStates[floor] = state;
            }

            OnStateChanged?.Invoke();
        }
    }
}
