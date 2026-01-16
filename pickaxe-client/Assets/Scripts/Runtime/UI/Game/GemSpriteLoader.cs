using UnityEngine;
using System.Collections.Generic;
using InfinitePickaxe.Client.Metadata;

namespace InfinitePickaxe.Client.UI.Game
{
    /// <summary>
    /// 보석 Sprite 로더 및 캐시
    /// Sprites/UI/Gems 폴더의 PNG 파일을 로드하고 캐싱
    /// </summary>
    public static class GemSpriteLoader
    {
        private const string GEMS_SPRITE_PATH = "Sprites/UI/Gems/";

        // Sprite 캐시 (gem_id -> Sprite)
        private static readonly Dictionary<uint, Sprite> spriteCache = new Dictionary<uint, Sprite>();

        // Sprite 캐시 (icon_name -> Sprite)
        private static readonly Dictionary<string, Sprite> spriteByNameCache = new Dictionary<string, Sprite>();
        private static GemMetaResolver metaResolver;

        /// <summary>
        /// 보석 ID로 Sprite 가져오기
        /// </summary>
        /// <param name="gemId">보석 메타 ID</param>
        /// <returns>로드된 Sprite 또는 null</returns>
        public static Sprite GetGemSprite(uint gemId)
        {
            // 캐시 확인
            if (spriteCache.TryGetValue(gemId, out var cachedSprite))
            {
                return cachedSprite;
            }

            // 메타데이터에서 아이콘 이름 조회
            var iconName = GetIconNameByGemId(gemId);
            if (string.IsNullOrEmpty(iconName))
            {
                Debug.LogWarning($"[GemSpriteLoader] Icon name not found for gem_id={gemId}");
                return null;
            }

            // 아이콘 이름으로 로드
            var sprite = LoadSpriteByName(iconName);

            // 캐시에 저장
            if (sprite != null)
            {
                spriteCache[gemId] = sprite;
            }

            return sprite;
        }

        /// <summary>
        /// 아이콘 이름으로 Sprite 가져오기
        /// </summary>
        /// <param name="iconName">아이콘 이름 (예: "gem_common_attack_speed")</param>
        /// <returns>로드된 Sprite 또는 null</returns>
        public static Sprite GetGemSpriteByName(string iconName)
        {
            if (string.IsNullOrEmpty(iconName))
            {
                return null;
            }

            return LoadSpriteByName(iconName);
        }

        /// <summary>
        /// 아이콘 이름으로 Sprite 로드 (캐싱)
        /// </summary>
        private static Sprite LoadSpriteByName(string iconName)
        {
            // 캐시 확인
            if (spriteByNameCache.TryGetValue(iconName, out var cachedSprite))
            {
                return cachedSprite;
            }

            // Resources에서 로드
            string path = GEMS_SPRITE_PATH + iconName;
            var sprite = Resources.Load<Sprite>(path);

            if (sprite == null)
            {
                Debug.LogWarning($"[GemSpriteLoader] Sprite not found at path: {path}");
            }
            else
            {
                // 캐시에 저장
                spriteByNameCache[iconName] = sprite;
                Debug.Log($"[GemSpriteLoader] Loaded sprite: {iconName}");
            }

            return sprite;
        }

        /// <summary>
        /// 메타데이터 기반 보석ID 주입
        /// </summary>
        private static string GetIconNameByGemId(uint gemId)
        {
            EnsureMetaResolver();

            if (metaResolver.TryGetDefinition(gemId, out var def) && !string.IsNullOrEmpty(def.Icon))
            {
                return def.Icon;
            }

            return null;
        }

        private static void EnsureMetaResolver()
        {
            if (metaResolver == null)
            {
                metaResolver = new GemMetaResolver();
                return;
            }

            if (MetaRepository.Loaded && metaResolver.AllDefinitions.Count == 0)
            {
                metaResolver.Reload();
            }
        }

        /// <summary>
        /// GemInfo에서 Sprite 가져오기 (편의 메서드)
        /// </summary>
        public static Sprite GetGemSprite(Infinitepickaxe.GemInfo gemInfo)
        {
            if (gemInfo == null)
            {
                return null;
            }

            // icon 필드가 있으면 우선 사용
            if (!string.IsNullOrEmpty(gemInfo.Icon))
            {
                return GetGemSpriteByName(gemInfo.Icon);
            }

            // 없으면 gem_id로 조회
            return GetGemSprite(gemInfo.GemId);
        }

        /// <summary>
        /// 캐시 클리어 (메모리 해제 필요 시)
        /// </summary>
        public static void ClearCache()
        {
            spriteCache.Clear();
            spriteByNameCache.Clear();
            Debug.Log("[GemSpriteLoader] Cache cleared");
        }
    }
}
