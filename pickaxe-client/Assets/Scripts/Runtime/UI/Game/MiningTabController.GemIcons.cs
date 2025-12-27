using UnityEngine;

namespace InfinitePickaxe.Client.UI.Game
{
    /// <summary>
    /// MiningTabController의 젬 아이콘 표시 기능
    /// </summary>
    public partial class MiningTabController
    {
        [Header("Gem Slot Icons")]
        [SerializeField] private GemSlotIconsView slot1GemIcons;
        [SerializeField] private GemSlotIconsView slot2GemIcons;
        [SerializeField] private GemSlotIconsView slot3GemIcons;
        [SerializeField] private GemSlotIconsView slot4GemIcons;

        /// <summary>
        /// 모든 곡괭이 슬롯의 젬 아이콘 업데이트
        /// RefreshData()에서 호출
        /// </summary>
        private void UpdateAllGemIcons()
        {
            UpdateGemIcons(slot1GemIcons, 0);
            UpdateGemIcons(slot2GemIcons, 1);
            UpdateGemIcons(slot3GemIcons, 2);
            UpdateGemIcons(slot4GemIcons, 3);
        }

        /// <summary>
        /// 특정 슬롯의 젬 아이콘 업데이트
        /// </summary>
        private void UpdateGemIcons(GemSlotIconsView view, uint slotIndex)
        {
            if (view == null) return;

            if (slotInfos.TryGetValue(slotIndex, out var slotInfo))
            {
                view.UpdateGemSlots(slotInfo.GemSlots);
            }
            else
            {
                view.UpdateGemSlots(null);
            }
        }
    }
}
