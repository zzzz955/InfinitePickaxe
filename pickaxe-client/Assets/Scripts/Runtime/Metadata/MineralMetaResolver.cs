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

        public void Reload()
        {
            initialized = false;
            mineralsById.Clear();
            InitializeFromMeta();
        }

        private void InitializeFromMeta()
        {
            if (initialized) return;

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

                if (!TryGetUInt(dict, out var id, "id", "mineral_id", "mineralId"))
                    continue;

                var meta = new MineralMeta
                {
                    Id = id,
                    Name = TryGetString(dict, out var name, "name", "label") ? name : $"광물 #{id}",
                    Hp = TryGetFloat(dict, out var hp, "hp", "hp_min", "min_hp", "minHp", "minHP", "mineral_hp") ? hp : 0f,
                    Gold = TryGetFloat(dict, out var gold, "gold", "reward", "gold_reward", "goldReward", "reward_gold") ? gold : 0f,
                    RecommendedMinDps = TryGetFloat(dict, out var min, "recommended_min_DPS", "recommended_min_dps", "recommendedMinDps", "min_dps", "minDps") ? min : 0f,
                    RecommendedMaxDps = TryGetFloat(dict, out var max, "recommended_max_DPS", "recommended_max_dps", "recommendedMaxDps", "max_dps", "maxDps") ? max : 0f,
                    Biome = TryGetString(dict, out var biome, "biome", "region") ? biome : string.Empty
                };

                mineralsById[id] = meta;
            }
            initialized = true;
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
            }

            value = 0;
            return false;
        }

        private static bool TryGetFloat(Dictionary<string, object> dict, out float value, params string[] keys)
        {
            foreach (var key in keys)
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
