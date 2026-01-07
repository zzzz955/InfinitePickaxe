using System;
using System.Collections.Generic;

namespace InfinitePickaxe.Client.Metadata
{
    public sealed class MineralInfoMetaResolver
    {
        private readonly Dictionary<uint, MineralInfoMeta> mineralsById = new Dictionary<uint, MineralInfoMeta>();
        private readonly List<MineralInfoMeta> minerals = new List<MineralInfoMeta>();
        private bool initialized;
        private bool warnedNoMeta;

        public MineralInfoMetaResolver()
        {
            InitializeFromMeta();
        }

        public IReadOnlyList<MineralInfoMeta> Minerals => minerals;

        public bool TryGetMineral(uint id, out MineralInfoMeta meta)
        {
            return mineralsById.TryGetValue(id, out meta);
        }

        public void Reload()
        {
            initialized = false;
            warnedNoMeta = false;
            mineralsById.Clear();
            minerals.Clear();
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

            if (!MetaRepository.Data.TryGetValue("minerals_info", out var obj) || obj is not List<object> list)
            {
                if (!warnedNoMeta)
                {
                    warnedNoMeta = true;
                    UnityEngine.Debug.LogWarning("MineralInfoMetaResolver: minerals_info section missing in meta_bundle.json.");
                }
                return;
            }

            foreach (var entry in list)
            {
                if (entry is not Dictionary<string, object> dict) continue;

                if (!TryGetUInt(dict, out var id, "id"))
                {
                    continue;
                }

                var meta = new MineralInfoMeta
                {
                    Id = id,
                    Name = TryGetString(dict, out var name, "name") ? name : string.Empty,
                    SpriteKey = TryGetString(dict, out var spriteKey, "sprite_key") ? spriteKey : string.Empty
                };

                mineralsById[id] = meta;
                minerals.Add(meta);
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

    public sealed class MineralInfoMeta
    {
        public uint Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SpriteKey { get; set; } = string.Empty;
    }
}
