using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InfinitePickaxe.Client.UI.Game
{
    public sealed class ItemSlotView : MonoBehaviour
    {
        [SerializeField] private Button slotButton;
        [SerializeField] private Image rarityFrameImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI countText;
        [SerializeField] private GameObject emptyState;
        [SerializeField] private GameObject selectedOutline;

        private ItemSlotData data;
        private Action<ItemSlotView> clickHandler;

        public ItemSlotData Data => data;
        public bool IsEmpty => data.ItemId == 0;

        private void Awake()
        {
            if (slotButton != null)
            {
                slotButton.onClick.RemoveAllListeners();
                slotButton.onClick.AddListener(() => clickHandler?.Invoke(this));
            }
        }

        public void Bind(ItemSlotData slotData, Sprite icon, Color frameColor, Color textColor, bool selected, Action<ItemSlotView> onClick)
        {
            data = slotData;
            clickHandler = onClick;

            bool hasItem = slotData.ItemId != 0;

            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = hasItem && icon != null;
            }

            if (rarityFrameImage != null)
            {
                rarityFrameImage.color = frameColor;
            }

            if (countText != null)
            {
                bool showCount = hasItem && slotData.Count > 1;
                countText.gameObject.SetActive(showCount);
                if (showCount)
                {
                    countText.text = slotData.Count.ToString("N0");
                    // countText.color = textColor;
                }
            }

            if (emptyState != null)
            {
                emptyState.SetActive(!hasItem);
            }

            if (slotButton != null)
            {
                slotButton.interactable = hasItem;
            }

            SetSelected(selected);
        }

        public void SetSelected(bool selected)
        {
            if (selectedOutline != null)
            {
                selectedOutline.SetActive(selected);
            }
        }
    }

    public struct ItemSlotData
    {
        public uint ItemId;
        public string InstanceId;
        public ulong Count;
        public ulong AcquiredAtMs;

        public bool IsInstance => !string.IsNullOrEmpty(InstanceId);
    }
}
