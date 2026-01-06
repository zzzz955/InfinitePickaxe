using System;
using System.Collections.Generic;
using Infinitepickaxe;

namespace InfinitePickaxe.Client.Core
{
    public sealed class AchievementStateCache
    {
        private static readonly Lazy<AchievementStateCache> Lazy = new Lazy<AchievementStateCache>(() => new AchievementStateCache());
        public static AchievementStateCache Instance => Lazy.Value;

        private readonly Dictionary<string, ulong> progressByType = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<uint, uint> chainSteps = new Dictionary<uint, uint>();

        public event Action OnProgressChanged;
        public event Action OnChainsChanged;

        public bool HasState { get; private set; }

        public IReadOnlyDictionary<string, ulong> ProgressByType => progressByType;
        public IReadOnlyDictionary<uint, uint> ChainSteps => chainSteps;

        private AchievementStateCache() { }

        public void ResetAll()
        {
            progressByType.Clear();
            chainSteps.Clear();
            HasState = false;
            OnProgressChanged?.Invoke();
            OnChainsChanged?.Invoke();
        }

        public void UpdateFromAchievementsResponse(AchievementsResponse response)
        {
            if (response == null) return;

            progressByType.Clear();
            chainSteps.Clear();

            if (response.Progresses != null)
            {
                foreach (var progress in response.Progresses)
                {
                    if (progress == null || string.IsNullOrWhiteSpace(progress.AchievementType)) continue;
                    progressByType[progress.AchievementType] = progress.CurrentValue;
                }
            }

            if (response.Chains != null)
            {
                foreach (var chain in response.Chains)
                {
                    if (chain == null || chain.ChainId == 0) continue;
                    chainSteps[chain.ChainId] = chain.LastClaimedStep;
                }
            }

            HasState = true;
            OnProgressChanged?.Invoke();
            OnChainsChanged?.Invoke();
        }

        public void UpdateFromProgressUpdate(AchievementProgressUpdate update)
        {
            if (update == null || string.IsNullOrWhiteSpace(update.AchievementType)) return;

            progressByType[update.AchievementType] = update.CurrentValue;
            HasState = true;
            OnProgressChanged?.Invoke();
        }

        public void ApplyClaimResult(AchievementClaimResult result)
        {
            if (result == null || !result.Success) return;
            if (result.ChainId == 0) return;

            chainSteps[result.ChainId] = result.ClaimedStep;
            HasState = true;
            OnChainsChanged?.Invoke();
        }

        public ulong GetProgressOrDefault(string achievementType)
        {
            if (string.IsNullOrWhiteSpace(achievementType)) return 0;
            return progressByType.TryGetValue(achievementType, out var value) ? value : 0;
        }

        public uint GetLastClaimedStep(uint chainId)
        {
            if (chainId == 0) return 0;
            return chainSteps.TryGetValue(chainId, out var value) ? value : 0;
        }
    }
}
