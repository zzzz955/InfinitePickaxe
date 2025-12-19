using System;
using System.Collections.Generic;
using Infinitepickaxe;

namespace InfinitePickaxe.Client.Core
{
    /// <summary>
    /// 서버에서 내려준 곡괭이(PickaxeSlotInfo) 상태를 전역으로 캐싱하고 브로드캐스트한다.
    /// Game 씬 어디서나 참조 가능하며, 실시간으로 업데이트된다.
    /// </summary>
    public sealed class PickaxeStateCache
    {
        private static readonly Lazy<PickaxeStateCache> Lazy = new Lazy<PickaxeStateCache>(() => new PickaxeStateCache());
        public static PickaxeStateCache Instance => Lazy.Value;

        private readonly Dictionary<uint, PickaxeSlotInfo> slots = new Dictionary<uint, PickaxeSlotInfo>();

        public event Action OnChanged;

        public ulong TotalDps { get; private set; }

        private PickaxeStateCache() { }

        public IReadOnlyDictionary<uint, PickaxeSlotInfo> Slots => slots;

        public bool TryGetSlot(uint index, out PickaxeSlotInfo info) => slots.TryGetValue(index, out info);

        public void UpdateFromSnapshot(UserDataSnapshot snapshot)
        {
            if (snapshot == null) return;
            MergeSlots(snapshot.PickaxeSlots);
            TotalDps = snapshot.TotalDps;
            RaiseChanged();
        }

        public void UpdateFromAllSlots(AllSlotsResponse response)
        {
            if (response == null) return;
            MergeSlots(response.Slots);
            TotalDps = response.TotalDps;
            RaiseChanged();
        }

        public void UpdateFromUpgradeResult(UpgradeResult result)
        {
            if (result == null) return;

            if (slots.TryGetValue(result.SlotIndex, out var existing) && existing != null)
            {
                existing.PityBonus = result.PityBonus;
                slots[result.SlotIndex] = existing;
            }

            if (!result.Success)
            {
                RaiseChanged();
                return;
            }

            var info = new PickaxeSlotInfo
            {
                SlotIndex = result.SlotIndex,
                Level = result.NewLevel,
                Tier = result.NewTier,
                AttackPower = result.NewAttackPower,
                AttackSpeedX100 = result.NewAttackSpeedX100,
                CriticalHitPercent = result.NewCriticalHitPercent,
                CriticalDamage = result.NewCriticalDamage,
                Dps = result.NewDps,
                PityBonus = result.PityBonus,
                IsUnlocked = true
            };

            slots[result.SlotIndex] = info;
            TotalDps = result.NewTotalDps;
            RaiseChanged();
        }

        private void MergeSlots(IEnumerable<PickaxeSlotInfo> list)
        {
            if (list == null) return;
            foreach (var s in list)
            {
                if (s == null) continue;
                slots[s.SlotIndex] = s;
            }
        }

        private void RaiseChanged() => OnChanged?.Invoke();
    }
}
