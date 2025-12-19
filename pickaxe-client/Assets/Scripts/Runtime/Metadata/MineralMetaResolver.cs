using System;
using System.Collections.Generic;
using System.Linq;

namespace InfinitePickaxe.Client.Metadata
{
    /// <summary>
    /// meta_bundle.json 내 minerals 테이블을 파싱해 ID -> 메타 데이터로 조회하는 헬퍼.
    /// 추천 DPS 범위(min/max)도 함께 노출한다.
    /// </summary>
    public sealed class MineralMetaResolver
    {
        private readonly Dictionary<uint, MineralMeta> mineralsById = new Dictionary<uint, MineralMeta>();
        private bool initialized;

        public MineralMetaResolver()
        {
            InitializeFromMeta();
        }

        public IReadOnlyCollection<MineralMeta> All => mineralsById.Values;

        public bool TryGetMineral(uint id, out MineralMeta meta)
        {
            return mineralsById.TryGetValue(id, out meta);
        }

        public string GetNameOrDefault(uint id, string fallback)
        {
            return mineralsById.TryGetValue(id, out var meta) ? meta.Name : fallback;
        }

        /// <summary>
        /// 현재 DPS에 추천되는 광물 ID 목록을 반환한다.
        /// </summary>
        public IEnumerable<uint> GetRecommendedMineralIds(float currentDps)
        {
            return mineralsById.Values
                .Where(m => m.RecommendedMinDps <= currentDps && currentDps <= m.RecommendedMaxDps)
                .Select(m => m.Id);
        }

        private void InitializeFromMeta()
        {
            if (initialized) return;
            initialized = true;

            if (!MetaRepository.Loaded || MetaRepository.Data == null)
            {
                return;
            }

            if (!MetaRepository.Data.TryGetValue("minerals", out var obj) || obj is not List<object> list)
            {
                return;
            }

            foreach (var entry in list)
            {
                if (entry is not Dictionary<string, object> dict)
                    continue;

                if (!TryGetUInt(dict, "id", out var id))
                    continue;

                var meta = new MineralMeta
                {
                    Id = id,
                    Name = TryGetString(dict, "name", out var name) ? name : $"광물 #{id}",
                    Hp = TryGetFloat(dict, "hp", out var hp) ? hp : 0f,
                    Gold = TryGetFloat(dict, "gold", out var gold) ? gold : 0f,
                    RecommendedMinDps = TryGetFloat(dict, "recommended_min_DPS", out var min) ? min : 0f,
                    RecommendedMaxDps = TryGetFloat(dict, "recommended_max_DPS", out var max) ? max : 0f,
                    Biome = TryGetString(dict, "biome", out var biome) ? biome : string.Empty
                };

                mineralsById[id] = meta;
            }
        }

        private static bool TryGetString(Dictionary<string, object> dict, string key, out string value)
        {
            if (dict.TryGetValue(key, out var obj) && obj != null)
            {
                value = obj.ToString();
                return true;
            }

            value = string.Empty;
            return false;
        }

        private static bool TryGetUInt(Dictionary<string, object> dict, string key, out uint value)
        {
            if (dict.TryGetValue(key, out var obj))
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
            }

            value = 0;
            return false;
        }

        private static bool TryGetFloat(Dictionary<string, object> dict, string key, out float value)
        {
            if (dict.TryGetValue(key, out var obj))
            {
                switch (obj)
                {
                    case float f:
                        value = f;
                        return true;
                    case double d:
                        value = (float)d;
                        return true;
                    case int i:
                        value = i;
                        return true;
                    case long l:
                        value = l;
                        return true;
                    case uint u:
                        value = u;
                        return true;
                    case string s when float.TryParse(s, out var parsed):
                        value = parsed;
                        return true;
                }
            }

            value = 0f;
            return false;
        }
    }

    public sealed class MineralMeta
    {
        public uint Id { get; set; }
        public string Name { get; set; }
        public float Hp { get; set; }
        public float Gold { get; set; }
        public float RecommendedMinDps { get; set; }
        public float RecommendedMaxDps { get; set; }
        public string Biome { get; set; }
    }
}
