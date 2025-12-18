using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

namespace InfinitePickaxe.Client.UI.Common
{
    /// <summary>
    /// UI용 스프라이트 아틀라스 캐시.
    /// - 한 번 로드한 스프라이트를 캐싱하여 드로우콜/할당 최소화
    /// - 에디터에서는 AssetDatabase를 통해 경로 기반 로드도 허용 (빌드 시엔 아틀라스 참조 사용)
    /// </summary>
    public static class SpriteAtlasCache
    {
        private static SpriteAtlas pickaxeAtlas;
        private static SpriteAtlas mineralAtlas;
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        private static Sprite fallbackSprite;

        public static void RegisterPickaxeAtlas(SpriteAtlas atlas)
        {
            if (atlas == null || pickaxeAtlas == atlas) return;
            pickaxeAtlas = atlas;
            ClearCacheWithPrefix("pickaxe_t");
        }

        public static void RegisterMineralAtlas(SpriteAtlas atlas)
        {
            if (atlas == null || mineralAtlas == atlas) return;
            mineralAtlas = atlas;
            ClearCacheWithPrefix("mineral_");
        }

        public static bool TryGetPickaxeSprite(uint tier, out Sprite sprite)
        {
            return TryGetSprite(pickaxeAtlas, $"pickaxe_t{tier}", out sprite);
        }

        public static Sprite GetPickaxeSprite(uint tier)
        {
            return TryGetPickaxeSprite(tier, out var sprite) ? sprite : GetFallbackSprite();
        }

        public static bool TryGetMineralSprite(uint mineralId, out Sprite sprite)
        {
            return TryGetSprite(mineralAtlas, $"mineral_{mineralId}", out sprite);
        }

        public static Sprite GetMineralSprite(uint mineralId)
        {
            return TryGetMineralSprite(mineralId, out var sprite) ? sprite : GetFallbackSprite();
        }

        public static Sprite GetFallbackSprite()
        {
            if (fallbackSprite != null) return fallbackSprite;

            var tex = Texture2D.whiteTexture;
            fallbackSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            fallbackSprite.name = "RuntimeWhiteSprite";
            return fallbackSprite;
        }

        private static bool TryGetSprite(SpriteAtlas atlas, string spriteName, out Sprite sprite)
        {
            if (!string.IsNullOrEmpty(spriteName) && cache.TryGetValue(spriteName, out sprite) && sprite != null)
            {
                return true;
            }

            sprite = atlas != null ? atlas.GetSprite(spriteName) : null;

#if UNITY_EDITOR
            if (sprite == null)
            {
                sprite = LoadFromAssetDatabase(spriteName);
            }
#endif

            if (sprite != null)
            {
                cache[spriteName] = sprite;
                return true;
            }

            return false;
        }

        private static void ClearCacheWithPrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix) || cache.Count == 0) return;

            var keysToRemove = new List<string>();
            foreach (var key in cache.Keys)
            {
                if (key.StartsWith(prefix))
                {
                    keysToRemove.Add(key);
                }
            }

            for (int i = 0; i < keysToRemove.Count; i++)
            {
                cache.Remove(keysToRemove[i]);
            }
        }

#if UNITY_EDITOR
        private static Sprite LoadFromAssetDatabase(string spriteName)
        {
            // 에디터 플레이 모드에서만 사용: 이름이 일치하는 스프라이트를 찾아 로드
            var guids = UnityEditor.AssetDatabase.FindAssets($"{spriteName} t:Sprite");
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith($"{spriteName}.png") || path.EndsWith($"{spriteName}.psd") || path.EndsWith($"{spriteName}.jpg"))
                {
                    var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite != null)
                    {
                        return sprite;
                    }
                }
            }

            return null;
        }
#endif
    }
}
