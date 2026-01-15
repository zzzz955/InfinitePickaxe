using System.Collections.Generic;
using UnityEngine;

namespace InfinitePickaxe.Client.UI.Game
{
    public static class ItemSpriteLoader
    {
        private const string ItemSpritePath = "Sprites/UI/Items/";
        private const string CurrencySpritePath = "Sprites/UI/Currency/";

        private static readonly Dictionary<string, Sprite> itemSpriteCache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Sprite> currencySpriteCache = new Dictionary<string, Sprite>();

        public static Sprite GetItemSprite(string spriteKey)
        {
            if (string.IsNullOrEmpty(spriteKey))
            {
                return null;
            }

            if (itemSpriteCache.TryGetValue(spriteKey, out var cached))
            {
                return cached;
            }

            var sprite = Resources.Load<Sprite>($"{ItemSpritePath}{spriteKey}");
            if (sprite != null)
            {
                itemSpriteCache[spriteKey] = sprite;
            }
            else
            {
                Debug.LogWarning($"[ItemSpriteLoader] Sprite not found: {ItemSpritePath}{spriteKey}");
            }

            return sprite;
        }

        public static Sprite GetCurrencySprite(string spriteKey)
        {
            if (string.IsNullOrEmpty(spriteKey))
            {
                return null;
            }

            if (currencySpriteCache.TryGetValue(spriteKey, out var cached))
            {
                return cached;
            }

            var sprite = Resources.Load<Sprite>($"{CurrencySpritePath}{spriteKey}");
            if (sprite != null)
            {
                currencySpriteCache[spriteKey] = sprite;
            }
            else
            {
                Debug.LogWarning($"[ItemSpriteLoader] Sprite not found: {CurrencySpritePath}{spriteKey}");
            }

            return sprite;
        }
    }
}
