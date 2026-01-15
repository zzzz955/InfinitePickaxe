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

        private uint rewardEntryId;
        private Action<uint> clickHandler;

        private void Awake()
        {
            if (optionButton != null)
            {
                optionButton.onClick.RemoveAllListeners();
                optionButton.onClick.AddListener(() => clickHandler?.Invoke(rewardEntryId));
            }
        }

        public void Bind(uint entryId, Sprite icon, ulong amount, Color frameColor, Color textColor, bool selected, Action<uint> onClick)
        {
            rewardEntryId = entryId;
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
                    countText.color = textColor;
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
