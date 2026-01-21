using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InfinitePickaxe.Client.UI.Game
{
    public sealed class ItemChoiceOptionView : MonoBehaviour
    {
        [SerializeField] private Button optionButton;
        [SerializeField] private Image rarityFrameImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI countText;
        [SerializeField] private GameObject selectedMark;

        public uint RewardEntryId { get; private set; }
        private Action<ItemChoiceOptionView> clickHandler;

        private void Awake()
        {
            if (optionButton != null)
            {
                optionButton.onClick.RemoveAllListeners();
                optionButton.onClick.AddListener(() => clickHandler?.Invoke(this));
            }

            if (selectedMark != null)
            {
                selectedMark.SetActive(false);
            }
        }

        public void Bind(uint entryId, Sprite icon, ulong amount, Color frameColor, Color textColor, bool selected, Action<ItemChoiceOptionView> onClick)
        {
            RewardEntryId = entryId;
            clickHandler = onClick;

            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }

            if (rarityFrameImage != null)
            {
                rarityFrameImage.color = frameColor;
            }

            if (countText != null)
            {
                bool showCount = amount > 1;
                countText.gameObject.SetActive(showCount);
                if (showCount)
                {
                    countText.text = amount.ToString("N0");
                    // countText.color = textColor;
                }
            }

            SetSelected(selected);
        }

        public void SetSelected(bool selected)
        {
            if (selectedMark != null)
            {
                selectedMark.SetActive(selected);
            }
        }
    }
}
