using System;
using Infinitepickaxe;

namespace InfinitePickaxe.Client.Core
{
    public sealed class UserResourceCache
    {
        private static readonly Lazy<UserResourceCache> Lazy = new Lazy<UserResourceCache>(() => new UserResourceCache());
        public static UserResourceCache Instance => Lazy.Value;

        public event Action OnChanged;

        public ulong? Gold { get; private set; }
        public uint? Crystal { get; private set; }
        public uint OfflineSeconds { get; private set; }
        public bool HasOfflineSeconds { get; private set; }

        private UserResourceCache() { }

        public void Reset()
        {
            bool changed = false;
            if (Gold.HasValue)
            {
                Gold = null;
                changed = true;
            }
            if (Crystal.HasValue)
            {
                Crystal = null;
                changed = true;
            }
            if (HasOfflineSeconds || OfflineSeconds != 0)
            {
                OfflineSeconds = 0;
                HasOfflineSeconds = false;
                changed = true;
            }
            if (changed)
            {
                RaiseChanged();
            }
        }

        public void UpdateFromSnapshot(UserDataSnapshot snapshot)
        {
            if (snapshot == null) return;

            bool changed = false;
            if (snapshot.Gold.HasValue)
            {
                changed |= SetGold(snapshot.Gold.Value);
            }
            if (snapshot.Crystal.HasValue)
            {
                changed |= SetCrystal(snapshot.Crystal.Value);
            }
            changed |= SetOfflineSeconds(snapshot.CurrentOfflineSeconds);

            if (changed)
            {
                RaiseChanged();
            }
        }

        public void UpdateCurrency(ulong? gold, uint? crystal)
        {
            bool changed = false;
            if (gold.HasValue)
            {
                changed |= SetGold(gold.Value);
            }
            if (crystal.HasValue)
            {
                changed |= SetCrystal(crystal.Value);
            }
            if (changed)
            {
                RaiseChanged();
            }
        }

        public void UpdateOfflineSeconds(uint seconds)
        {
            if (SetOfflineSeconds(seconds))
            {
                RaiseChanged();
            }
        }

        public bool TryGetOfflineSeconds(out uint seconds)
        {
            seconds = OfflineSeconds;
            return HasOfflineSeconds;
        }

        private bool SetGold(ulong value)
        {
            if (Gold.HasValue && Gold.Value == value)
            {
                return false;
            }
            Gold = value;
            return true;
        }

        private bool SetCrystal(uint value)
        {
            if (Crystal.HasValue && Crystal.Value == value)
            {
                return false;
            }
            Crystal = value;
            return true;
        }

        private bool SetOfflineSeconds(uint value)
        {
            if (HasOfflineSeconds && OfflineSeconds == value)
            {
                return false;
            }
            OfflineSeconds = value;
            HasOfflineSeconds = true;
            return true;
        }

        private void RaiseChanged() => OnChanged?.Invoke();
    }
}
