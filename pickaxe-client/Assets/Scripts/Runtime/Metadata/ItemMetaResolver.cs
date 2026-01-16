using System;
using System.Collections.Generic;

namespace InfinitePickaxe.Client.Metadata
{
    public sealed class ItemMetaResolver
    {
        private readonly Dictionary<uint, ItemInfoMeta> itemsById = new Dictionary<uint, ItemInfoMeta>();
        private ItemInventoryConfig inventoryConfig = new ItemInventoryConfig();
        private bool initialized;
        private bool warnedNoMeta;

        public ItemMetaResolver()
        {
            InitializeFromMeta();
        }

        public bool HasData => itemsById.Count > 0;
        public ItemInventoryConfig InventoryConfig => inventoryConfig;

        public bool TryGetItem(uint itemId, out ItemInfoMeta meta)
        {
            return itemsById.TryGetValue(itemId, out meta);
        }

        public void Reload()
        {
            initialized = false;
            warnedNoMeta = false;
            itemsById.Clear();
            inventoryConfig = new ItemInventoryConfig();
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

            LoadItemInfo();
            LoadInventoryConfig();
        }

        private void LoadItemInfo()
        {
            if (!MetaRepository.Data.TryGetValue("item_info", out var obj) || obj is not List<object> list)
            {
                if (!warnedNoMeta)
                {
                    warnedNoMeta = true;
                    UnityEngine.Debug.LogWarning("ItemMetaResolver: item_info 메타가 없습니다.");
                }
                return;
            }

            foreach (var entry in list)
            {
                if (entry is not Dictionary<string, object> dict) continue;

                if (!TryGetUInt(dict, out var itemId, "item_id")) continue;
                if (itemId == 0) continue;

                var meta = new ItemInfoMeta
                {
                    ItemId = itemId,
                    ItemType = TryGetString(dict, out var itemType, "item_type") ? itemType : string.Empty,
                    SpriteKey = TryGetString(dict, out var spriteKey, "sprite_key") ? spriteKey : string.Empty,
                    RarityId = TryGetUInt(dict, out var rarityId, "rarity_id") ? rarityId : 0,
                    DisplayName = TryGetString(dict, out var displayName, "display_name") ? displayName : string.Empty,
                    Stackable = TryGetBool(dict, out var stackable, "stackable") && stackable,
                    MaxStack = TryGetUInt(dict, out var maxStack, "max_stack") ? maxStack : 0,
                    UseActionType = TryGetString(dict, out var useActionType, "use_action_type") ? useActionType : string.Empty,
                    UseActionRefId = TryGetUInt(dict, out var useActionRefId, "use_action_ref_id") ? useActionRefId : 0,
                    Description = TryGetString(dict, out var description, "description") ? description : string.Empty
                };

                itemsById[itemId] = meta;
            }
        }

        private void LoadInventoryConfig()
        {
            if (!MetaRepository.Data.TryGetValue("item_inventory", out var obj) || obj is not Dictionary<string, object> dict)
            {
                return;
            }

            inventoryConfig = new ItemInventoryConfig
            {
                BaseCapacity = TryGetUInt(dict, out var baseCapacity, "base_capacity") ? baseCapacity : 0,
                MaxCapacity = TryGetUInt(dict, out var maxCapacity, "max_capacity") ? maxCapacity : 0,
                ExpandStep = TryGetUInt(dict, out var expandStep, "expand_step") ? expandStep : 0,
                ExpandCost = TryGetUInt(dict, out var expandCost, "expand_cost") ? expandCost : 0
            };
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

        private static bool TryGetBool(Dictionary<string, object> dict, out bool value, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (dict.TryGetValue(key, out var obj) && TryConvertToBool(obj, out value))
                {
                    return true;
                }
            }

            value = false;
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

        private static bool TryConvertToBool(object obj, out bool value)
        {
            switch (obj)
            {
                case bool b:
                    value = b;
                    return true;
                case int i:
                    value = i != 0;
                    return true;
                case long l:
                    value = l != 0;
                    return true;
                case uint u:
                    value = u != 0;
                    return true;
                case ulong ul:
                    value = ul != 0;
                    return true;
                case string s when bool.TryParse(s, out var parsed):
                    value = parsed;
                    return true;
                case string s when int.TryParse(s, out var parsedInt):
                    value = parsedInt != 0;
                    return true;
            }

            value = false;
            return false;
        }
    }

    public sealed class ItemInfoMeta
    {
        public uint ItemId { get; set; }
        public string ItemType { get; set; } = string.Empty;
        public string SpriteKey { get; set; } = string.Empty;
        public uint RarityId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public bool Stackable { get; set; }
        public uint MaxStack { get; set; }
        public string UseActionType { get; set; } = string.Empty;
        public uint UseActionRefId { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public sealed class ItemInventoryConfig
    {
        public uint BaseCapacity { get; set; }
        public uint MaxCapacity { get; set; }
        public uint ExpandStep { get; set; }
        public uint ExpandCost { get; set; }
    }
}
