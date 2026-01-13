using System;
using System.Collections.Generic;

namespace InfinitePickaxe.Client.Metadata
{
    public sealed class RewardPackageMetaResolver
    {
        private readonly Dictionary<uint, RewardPackageMeta> packagesById = new Dictionary<uint, RewardPackageMeta>();
        private readonly Dictionary<uint, List<RewardPackageEntryMeta>> entriesByPackage = new Dictionary<uint, List<RewardPackageEntryMeta>>();
        private bool initialized;
        private bool warnedNoMeta;

        public RewardPackageMetaResolver()
        {
            InitializeFromMeta();
        }

        public bool HasData => packagesById.Count > 0;

        public bool TryGetPackage(uint packageId, out RewardPackageMeta meta)
        {
            return packagesById.TryGetValue(packageId, out meta);
        }

        public bool TryGetEntries(uint packageId, out IReadOnlyList<RewardPackageEntryMeta> entries)
        {
            if (entriesByPackage.TryGetValue(packageId, out var list))
            {
                entries = list;
                return true;
            }

            entries = Array.Empty<RewardPackageEntryMeta>();
            return false;
        }

        public void Reload()
        {
            initialized = false;
            warnedNoMeta = false;
            packagesById.Clear();
            entriesByPackage.Clear();
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

            LoadPackages();
            LoadEntries();
        }

        private void LoadPackages()
        {
            if (!MetaRepository.Data.TryGetValue("reward_packages", out var obj) || obj is not List<object> list)
            {
                if (!warnedNoMeta)
                {
                    warnedNoMeta = true;
                    UnityEngine.Debug.LogWarning("RewardPackageMetaResolver: reward_packages 섹션이 없습니다.");
                }
                return;
            }

            foreach (var entry in list)
            {
                if (entry is not Dictionary<string, object> dict) continue;

                if (!TryGetUInt(dict, out var packageId, "package_id")) continue;
                if (packageId == 0) continue;

                var meta = new RewardPackageMeta
                {
                    PackageId = packageId,
                    Mode = TryGetString(dict, out var mode, "mode") ? mode : string.Empty,
                    RollCount = TryGetUInt(dict, out var rollCount, "roll_count") ? rollCount : 1,
                    Description = TryGetString(dict, out var description, "description") ? description : string.Empty
                };

                packagesById[packageId] = meta;
            }
        }

        private void LoadEntries()
        {
            if (!MetaRepository.Data.TryGetValue("reward_package_entries", out var obj) || obj is not List<object> list)
            {
                return;
            }

            foreach (var entry in list)
            {
                if (entry is not Dictionary<string, object> dict) continue;

                if (!TryGetUInt(dict, out var packageId, "package_id")) continue;
                if (packageId == 0) continue;
                if (!TryGetUInt(dict, out var entryId, "entry_id")) continue;
                if (entryId == 0) continue;

                var meta = new RewardPackageEntryMeta
                {
                    PackageId = packageId,
                    EntryId = entryId,
                    RewardType = TryGetString(dict, out var rewardType, "reward_type") ? rewardType : string.Empty,
                    RewardRefId = TryGetUInt(dict, out var rewardRefId, "reward_ref_id") ? rewardRefId : 0,
                    Amount = TryGetULong(dict, out var amount, "amount") ? amount : 0,
                    Weight = TryGetUInt(dict, out var weight, "weight") ? weight : 0,
                    GroupId = TryGetUInt(dict, out var groupId, "group_id") ? groupId : 0
                };

                if (!entriesByPackage.TryGetValue(packageId, out var listByPackage))
                {
                    listByPackage = new List<RewardPackageEntryMeta>();
                    entriesByPackage[packageId] = listByPackage;
                }

                listByPackage.Add(meta);
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

    public sealed class RewardPackageMeta
    {
        public uint PackageId { get; set; }
        public string Mode { get; set; } = string.Empty;
        public uint RollCount { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public sealed class RewardPackageEntryMeta
    {
        public uint PackageId { get; set; }
        public uint EntryId { get; set; }
        public string RewardType { get; set; } = string.Empty;
        public uint RewardRefId { get; set; }
        public ulong Amount { get; set; }
        public uint Weight { get; set; }
        public uint GroupId { get; set; }
    }
}
