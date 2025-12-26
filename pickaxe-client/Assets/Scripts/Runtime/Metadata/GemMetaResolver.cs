using System;
using System.Collections.Generic;
using System.Linq;

namespace InfinitePickaxe.Client.Metadata
{
    /// <summary>
    /// meta_bundle.json 내 gem 관련 테이블들을 파싱해 조회하는 헬퍼.
    /// </summary>
    public sealed class GemMetaResolver
    {
        private readonly Dictionary<uint, GemTypeMeta> typesById = new Dictionary<uint, GemTypeMeta>();
        private readonly Dictionary<uint, GemGradeMeta> gradesById = new Dictionary<uint, GemGradeMeta>();
        private readonly Dictionary<uint, GemDefinitionMeta> definitionsById = new Dictionary<uint, GemDefinitionMeta>();
        private readonly Dictionary<uint, GemGachaRateMeta> gachaRatesByGrade = new Dictionary<uint, GemGachaRateMeta>();
        private readonly Dictionary<uint, GemConversionCostMeta> conversionCostsByGrade = new Dictionary<uint, GemConversionCostMeta>();
        private readonly Dictionary<uint, GemDiscardRewardMeta> discardRewardsByGrade = new Dictionary<uint, GemDiscardRewardMeta>();
        private readonly Dictionary<string, GemSynthesisRuleMeta> synthesisRules = new Dictionary<string, GemSynthesisRuleMeta>();
        private readonly Dictionary<uint, uint> slotUnlockCostsBySlot = new Dictionary<uint, uint>();

        private bool initialized;

        // gem_gacha 설정
        public uint SinglePullCost { get; private set; }
        public uint MultiPullCost { get; private set; }
        public uint MultiPullCount { get; private set; }

        // gem_inventory 설정
        public uint BaseCapacity { get; private set; }
        public uint MaxCapacity { get; private set; }
        public uint ExpandStep { get; private set; }
        public uint ExpandCost { get; private set; }

        public GemMetaResolver()
        {
            InitializeFromMeta();
        }

        public IReadOnlyCollection<GemTypeMeta> AllTypes => typesById.Values;
        public IReadOnlyCollection<GemGradeMeta> AllGrades => gradesById.Values;
        public IReadOnlyCollection<GemDefinitionMeta> AllDefinitions => definitionsById.Values;

        public bool TryGetType(uint id, out GemTypeMeta meta) => typesById.TryGetValue(id, out meta);
        public bool TryGetGrade(uint id, out GemGradeMeta meta) => gradesById.TryGetValue(id, out meta);
        public bool TryGetDefinition(uint id, out GemDefinitionMeta meta) => definitionsById.TryGetValue(id, out meta);
        public bool TryGetGachaRate(uint gradeId, out GemGachaRateMeta meta) => gachaRatesByGrade.TryGetValue(gradeId, out meta);
        public bool TryGetConversionCost(uint gradeId, out GemConversionCostMeta meta) => conversionCostsByGrade.TryGetValue(gradeId, out meta);
        public bool TryGetDiscardReward(uint gradeId, out GemDiscardRewardMeta meta) => discardRewardsByGrade.TryGetValue(gradeId, out meta);
        public bool TryGetSlotUnlockCost(uint slotIndex, out uint cost) => slotUnlockCostsBySlot.TryGetValue(slotIndex, out cost);

        public bool TryGetSynthesisRule(string fromGrade, out GemSynthesisRuleMeta meta)
        {
            return synthesisRules.TryGetValue(fromGrade, out meta);
        }

        public void Reload()
        {
            initialized = false;
            typesById.Clear();
            gradesById.Clear();
            definitionsById.Clear();
            gachaRatesByGrade.Clear();
            conversionCostsByGrade.Clear();
            discardRewardsByGrade.Clear();
            synthesisRules.Clear();
            slotUnlockCostsBySlot.Clear();
            SinglePullCost = 0;
            MultiPullCost = 0;
            MultiPullCount = 0;
            BaseCapacity = 0;
            MaxCapacity = 0;
            ExpandStep = 0;
            ExpandCost = 0;
            InitializeFromMeta();
        }

        private void InitializeFromMeta()
        {
            if (initialized) return;

            if (!MetaRepository.Loaded || MetaRepository.Data == null)
            {
                return;
            }

            LoadGemTypes();
            LoadGemGrades();
            LoadGemDefinitions();
            LoadGemGacha();
            LoadGemConversion();
            LoadGemDiscard();
            LoadGemInventory();
            LoadGemSynthesisRules();
            LoadGemSlotUnlockCosts();

            initialized = true;
        }

        private void LoadGemTypes()
        {
            if (!MetaRepository.Data.TryGetValue("gem_types", out var obj) || obj is not List<object> list)
            {
                return;
            }

            foreach (var entry in list)
            {
                if (entry is not Dictionary<string, object> dict) continue;

                if (!TryGetUInt(dict, out var id, "id")) continue;

                var meta = new GemTypeMeta
                {
                    Id = id,
                    Type = TryGetString(dict, out var type, "type") ? type : string.Empty,
                    DisplayName = TryGetString(dict, out var displayName, "display_name") ? displayName : string.Empty,
                    Description = TryGetString(dict, out var description, "description") ? description : string.Empty,
                    StatKey = TryGetString(dict, out var statKey, "stat_key") ? statKey : string.Empty
                };

                typesById[id] = meta;
            }
        }

        private void LoadGemGrades()
        {
            if (!MetaRepository.Data.TryGetValue("gem_grades", out var obj) || obj is not List<object> list)
            {
                return;
            }

            foreach (var entry in list)
            {
                if (entry is not Dictionary<string, object> dict) continue;

                if (!TryGetUInt(dict, out var id, "id")) continue;

                var meta = new GemGradeMeta
                {
                    Id = id,
                    Grade = TryGetString(dict, out var grade, "grade") ? grade : string.Empty,
                    DisplayName = TryGetString(dict, out var displayName, "display_name") ? displayName : string.Empty
                };

                gradesById[id] = meta;
            }
        }

        private void LoadGemDefinitions()
        {
            if (!MetaRepository.Data.TryGetValue("gem_definitions", out var obj) || obj is not List<object> list)
            {
                return;
            }

            foreach (var entry in list)
            {
                if (entry is not Dictionary<string, object> dict) continue;

                if (!TryGetUInt(dict, out var gemId, "gem_id")) continue;

                var meta = new GemDefinitionMeta
                {
                    GemId = gemId,
                    GradeId = TryGetUInt(dict, out var gradeId, "grade_id") ? gradeId : 0,
                    TypeId = TryGetUInt(dict, out var typeId, "type_id") ? typeId : 0,
                    Name = TryGetString(dict, out var name, "name") ? name : string.Empty,
                    Icon = TryGetString(dict, out var icon, "icon") ? icon : string.Empty,
                    StatMultiplier = TryGetFloat(dict, out var multiplier, "stat_multiplier") ? multiplier : 1.0f
                };

                definitionsById[gemId] = meta;
            }
        }

        private void LoadGemGacha()
        {
            if (!MetaRepository.Data.TryGetValue("gem_gacha", out var obj) || obj is not Dictionary<string, object> dict)
            {
                return;
            }

            SinglePullCost = TryGetUInt(dict, out var single, "single_pull_cost") ? single : 0;
            MultiPullCost = TryGetUInt(dict, out var multi, "multi_pull_cost") ? multi : 0;
            MultiPullCount = TryGetUInt(dict, out var count, "multi_pull_count") ? count : 0;

            if (dict.TryGetValue("grade_rates", out var ratesObj) && ratesObj is List<object> ratesList)
            {
                foreach (var entry in ratesList)
                {
                    if (entry is not Dictionary<string, object> rateDict) continue;

                    if (!TryGetUInt(rateDict, out var gradeId, "grade_id")) continue;

                    var rateMeta = new GemGachaRateMeta
                    {
                        GradeId = gradeId,
                        RatePercent = TryGetFloat(rateDict, out var rate, "rate_percent") ? rate : 0f
                    };

                    gachaRatesByGrade[gradeId] = rateMeta;
                }
            }
        }

        private void LoadGemConversion()
        {
            if (!MetaRepository.Data.TryGetValue("gem_conversion", out var obj) || obj is not List<object> list)
            {
                return;
            }

            foreach (var entry in list)
            {
                if (entry is not Dictionary<string, object> dict) continue;

                if (!TryGetUInt(dict, out var gradeId, "grade_id")) continue;

                var meta = new GemConversionCostMeta
                {
                    GradeId = gradeId,
                    RandomCost = TryGetUInt(dict, out var random, "random_cost") ? random : 0,
                    FixedCost = TryGetUInt(dict, out var fixed_, "fixed_cost") ? fixed_ : 0
                };

                conversionCostsByGrade[gradeId] = meta;
            }
        }

        private void LoadGemDiscard()
        {
            if (!MetaRepository.Data.TryGetValue("gem_discard", out var obj) || obj is not List<object> list)
            {
                return;
            }

            foreach (var entry in list)
            {
                if (entry is not Dictionary<string, object> dict) continue;

                if (!TryGetUInt(dict, out var gradeId, "grade_id")) continue;

                var meta = new GemDiscardRewardMeta
                {
                    GradeId = gradeId,
                    CrystalReward = TryGetUInt(dict, out var reward, "crystal_reward") ? reward : 0
                };

                discardRewardsByGrade[gradeId] = meta;
            }
        }

        private void LoadGemInventory()
        {
            if (!MetaRepository.Data.TryGetValue("gem_inventory", out var obj) || obj is not Dictionary<string, object> dict)
            {
                return;
            }

            BaseCapacity = TryGetUInt(dict, out var base_, "base_capacity") ? base_ : 0;
            MaxCapacity = TryGetUInt(dict, out var max, "max_capacity") ? max : 0;
            ExpandStep = TryGetUInt(dict, out var step, "expand_step") ? step : 0;
            ExpandCost = TryGetUInt(dict, out var cost, "expand_cost") ? cost : 0;
        }

        private void LoadGemSynthesisRules()
        {
            if (!MetaRepository.Data.TryGetValue("gem_synthesis_rules", out var obj) || obj is not List<object> list)
            {
                return;
            }

            foreach (var entry in list)
            {
                if (entry is not Dictionary<string, object> dict) continue;

                if (!TryGetString(dict, out var fromGrade, "from_grade")) continue;

                var meta = new GemSynthesisRuleMeta
                {
                    FromGrade = fromGrade,
                    ToGrade = TryGetString(dict, out var toGrade, "to_grade") ? toGrade : string.Empty,
                    SuccessRatePercent = TryGetFloat(dict, out var rate, "success_rate_percent") ? rate : 0f
                };

                synthesisRules[fromGrade] = meta;
            }
        }

        private void LoadGemSlotUnlockCosts()
        {
            if (!MetaRepository.Data.TryGetValue("gem_slot_unlock_costs", out var obj) || obj is not List<object> list)
            {
                return;
            }

            foreach (var entry in list)
            {
                if (entry is not Dictionary<string, object> dict) continue;

                if (!TryGetUInt(dict, out var slotIndex, "slot_index")) continue;
                if (!TryGetUInt(dict, out var cost, "unlock_cost_crystal")) continue;

                slotUnlockCostsBySlot[slotIndex] = cost;
            }
        }

        #region Helper Methods

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

        private static bool TryGetFloat(Dictionary<string, object> dict, out float value, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (dict.TryGetValue(key, out var obj) && TryConvertToFloat(obj, out value))
                {
                    return true;
                }
            }

            value = 0f;
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

        private static bool TryConvertToFloat(object obj, out float value)
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
                case ulong ul:
                    value = ul;
                    return true;
                case string s when float.TryParse(s, out var parsed):
                    value = parsed;
                    return true;
            }

            value = 0f;
            return false;
        }

        #endregion
    }

    #region Meta Data Classes

    public sealed class GemTypeMeta
    {
        public uint Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string StatKey { get; set; } = string.Empty;
    }

    public sealed class GemGradeMeta
    {
        public uint Id { get; set; }
        public string Grade { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public sealed class GemDefinitionMeta
    {
        public uint GemId { get; set; }
        public uint GradeId { get; set; }
        public uint TypeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public float StatMultiplier { get; set; }
    }

    public sealed class GemGachaRateMeta
    {
        public uint GradeId { get; set; }
        public float RatePercent { get; set; }
    }

    public sealed class GemConversionCostMeta
    {
        public uint GradeId { get; set; }
        public uint RandomCost { get; set; }
        public uint FixedCost { get; set; }
    }

    public sealed class GemDiscardRewardMeta
    {
        public uint GradeId { get; set; }
        public uint CrystalReward { get; set; }
    }

    public sealed class GemSynthesisRuleMeta
    {
        public string FromGrade { get; set; } = string.Empty;
        public string ToGrade { get; set; } = string.Empty;
        public float SuccessRatePercent { get; set; }
    }

    #endregion
}
