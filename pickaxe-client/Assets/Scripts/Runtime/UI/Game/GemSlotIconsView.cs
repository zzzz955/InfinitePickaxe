using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace InfinitePickaxe.Client.UI.Game
{
    /// <summary>
    /// Pickaxe Slot 버튼에 표시되는 젬 아이콘 그리드 (최대 6개)
    /// </summary>
    public sealed class GemSlotIconsView : MonoBehaviour
    {
        [Header("Gem Icon Slots")]
        [SerializeField] private Image gemIcon1;
        [SerializeField] private Image gemIcon2;
        [SerializeField] private Image gemIcon3;
        [SerializeField] private Image gemIcon4;
        [SerializeField] private Image gemIcon5;
        [SerializeField] private Image gemIcon6;

        [Header("Visual Settings")]
        [SerializeField] private Color equippedColor = Color.white;

        private readonly Image[] gemIcons = new Image[6];
        private bool initialized;

        private void Awake()
        {
            InitializeIcons();
        }

        private void InitializeIcons()
        {
            if (initialized) return;

            gemIcons[0] = gemIcon1;
            gemIcons[1] = gemIcon2;
            gemIcons[2] = gemIcon3;
            gemIcons[3] = gemIcon4;
            gemIcons[4] = gemIcon5;
            gemIcons[5] = gemIcon6;

            initialized = true;
        }

        /// <summary>
        /// 젬 슬롯 상태 업데이트
        /// </summary>
        /// <param name="slots">GemSlotInfo 리스트 (최대 6개)</param>
        public void UpdateGemSlots(IReadOnlyList<Infinitepickaxe.GemSlotInfo> slots)
        {
            InitializeIcons();

            if (slots == null || slots.Count == 0)
            {
                SetAllEmpty();
                return;
            }

            for (int i = 0; i < 6; i++)
            {
                var icon = gemIcons[i];
                if (icon == null) continue;

                if (i < slots.Count)
                {
                    var slot = slots[i];
                    UpdateSlotIcon(icon, slot);
                }
                else
                {
                    SetEmpty(icon);
                }
            }
        }

        /// <summary>
        /// 특정 슬롯 아이콘 업데이트
        /// </summary>
        private void UpdateSlotIcon(Image icon, Infinitepickaxe.GemSlotInfo slot)
        {
            if (icon == null || slot == null) return;

            if (!slot.IsUnlocked)
            {
                SetLocked(icon);
                return;
            }

            if (slot.EquippedGem != null && !string.IsNullOrWhiteSpace(slot.EquippedGem.GemInstanceId))
            {
                SetEquipped(icon, slot.EquippedGem);
            }
            else
            {
                SetEmpty(icon);
            }
        }

        /// <summary>
        /// 장착된 젬 아이콘 표시
        /// </summary>
        private void SetEquipped(Image icon, Infinitepickaxe.GemInfo gem)
        {
            if (icon == null || gem == null) return;

            // 젬 ID로 스프라이트 가져오기 (GemMetaResolver 또는 SpriteAtlasCache 활용)
            var sprite = GetGemSprite(gem.GemId);

            icon.sprite = sprite;
            icon.color = equippedColor;
            icon.enabled = sprite != null; // 스프라이트가 있을 때만 표시
        }

        /// <summary>
        /// 빈 슬롯 숨김 처리 (장착되지 않은 슬롯은 표시하지 않음)
        /// </summary>
        private void SetEmpty(Image icon)
        {
            if (icon == null) return;

            icon.sprite = null;
            icon.enabled = false; // 빈 슬롯은 숨김
        }

        /// <summary>
        /// 잠긴 슬롯 숨김 처리 (잠긴 슬롯은 표시하지 않음)
        /// </summary>
        private void SetLocked(Image icon)
        {
            if (icon == null) return;

            icon.sprite = null;
            icon.enabled = false; // 잠긴 슬롯도 숨김
        }

        /// <summary>
        /// 모든 슬롯을 빈 상태로 초기화
        /// </summary>
        private void SetAllEmpty()
        {
            for (int i = 0; i < 6; i++)
            {
                if (gemIcons[i] != null)
                {
                    SetEmpty(gemIcons[i]);
                }
            }
        }

        /// <summary>
        /// 젬 스프라이트 가져오기 (추후 SpriteAtlasCache 또는 GemMetaResolver와 연동)
        /// </summary>
        private Sprite GetGemSprite(uint gemId)
        {
            // TODO: SpriteAtlasCache.GetGemSprite(gemId) 구현 후 연동
            // 현재는 null 반환 (emptySlotSprite 사용)
            return null;
        }
    }
}
