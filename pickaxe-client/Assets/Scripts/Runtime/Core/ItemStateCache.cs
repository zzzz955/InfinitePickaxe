using System;
using System.Collections.Generic;
using System.Linq;
using Infinitepickaxe;
using InfinitePickaxe.Client.Metadata;

namespace InfinitePickaxe.Client.Core
{
    public sealed class ItemStateCache
    {
        private static readonly Lazy<ItemStateCache> Lazy = new Lazy<ItemStateCache>(() => new ItemStateCache());
        public static ItemStateCache Instance => Lazy.Value;

        public event Action OnInventoryChanged;

        private readonly Dictionary<uint, ulong> stackCounts = new Dictionary<uint, ulong>();
        private readonly Dictionary<string, ItemInstanceEntry> instancesById = new Dictionary<string, ItemInstanceEntry>(StringComparer.Ordinal);

        public uint Capacity { get; private set; }
        public uint UsedSlots { get; private set; }
        public bool HasData { get; private set; }

        public IReadOnlyDictionary<uint, ulong> Stacks => stackCounts;
        public IReadOnlyCollection<ItemInstanceEntry> Instances => instancesById.Values;

        private ItemStateCache() { }

        public void ResetAll()
        {
            stackCounts.Clear();
            instancesById.Clear();
            Capacity = 0;
            UsedSlots = 0;
            HasData = false;
            RaiseChanged();
        }

        public void UpdateFromResponse(ItemInventoryResponse response)
        {
            if (response == null) return;

            stackCounts.Clear();
            instancesById.Clear();

            foreach (var stack in response.Stacks)
            {
                if (stack == null || stack.ItemId == 0 || stack.Count == 0) continue;
                stackCounts[stack.ItemId] = stack.Count;
            }

            foreach (var instance in response.Instances)
            {
                if (instance == null || string.IsNullOrEmpty(instance.ItemInstanceId) || instance.ItemId == 0) continue;
                instancesById[instance.ItemInstanceId] = instance;
            }

            Capacity = response.CurrentCapacity;
            UsedSlots = response.UsedSlots > 0 ? response.UsedSlots : CalculateUsedSlots();
            HasData = true;
            RaiseChanged();
        }

        public void ApplyInventoryExpandResult(ItemInventoryExpandResult result)
        {
            if (result == null || !result.Success) return;
            if (result.NewCapacity > 0)
            {
                Capacity = result.NewCapacity;
            }
            UsedSlots = CalculateUsedSlots();
            RaiseChanged();
        }

        public void ApplyUseItemResult(UseItemResult result, ItemMetaResolver itemMetaResolver = null)
        {
            if (result == null || !result.Success) return;

            if (result.ItemId > 0 && result.CountUsed > 0)
            {
                RemoveUsedItems(result.ItemId, result.CountUsed, itemMetaResolver);
            }

            var instanceItemIds = new HashSet<uint>();
            foreach (var instance in result.ItemInstances)
            {
                if (instance == null || string.IsNullOrEmpty(instance.ItemInstanceId) || instance.ItemId == 0) continue;
                instancesById[instance.ItemInstanceId] = instance;
                instanceItemIds.Add(instance.ItemId);
            }

            foreach (var reward in result.Rewards)
            {
                if (reward == null || reward.RewardType != RewardType.Item) continue;
                if (!uint.TryParse(reward.RewardKey, out var rewardItemId)) continue;
                if (reward.Amount == 0) continue;
                if (instanceItemIds.Contains(rewardItemId)) continue;

                if (stackCounts.TryGetValue(rewardItemId, out var count))
                {
                    stackCounts[rewardItemId] = count + reward.Amount;
                }
                else
                {
                    stackCounts[rewardItemId] = reward.Amount;
                }
            }

            UsedSlots = CalculateUsedSlots();
            HasData = true;
            RaiseChanged();
        }

        private void RemoveUsedItems(uint itemId, uint countUsed, ItemMetaResolver itemMetaResolver)
        {
            bool treatAsStack = false;

            if (stackCounts.ContainsKey(itemId))
            {
                treatAsStack = true;
            }
            else if (itemMetaResolver != null && itemMetaResolver.TryGetItem(itemId, out var meta))
            {
                treatAsStack = meta.Stackable;
            }

            if (treatAsStack)
            {
                if (stackCounts.TryGetValue(itemId, out var current))
                {
                    ulong remaining = current > countUsed ? current - countUsed : 0;
                    if (remaining > 0)
                    {
                        stackCounts[itemId] = remaining;
                    }
                    else
                    {
                        stackCounts.Remove(itemId);
                    }
                }
                return;
            }

            var instances = instancesById.Values
                .Where(x => x != null && x.ItemId == itemId)
                .OrderBy(x => x.AcquiredAtMs)
                .ThenBy(x => x.ItemInstanceId)
                .ToList();

            int removeCount = Math.Min((int)countUsed, instances.Count);
            for (int i = 0; i < removeCount; i++)
            {
                instancesById.Remove(instances[i].ItemInstanceId);
            }
        }

        private uint CalculateUsedSlots()
        {
            return (uint)(stackCounts.Count + instancesById.Count);
        }

        private void RaiseChanged() => OnInventoryChanged?.Invoke();
    }
}
