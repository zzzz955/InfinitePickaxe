using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InfinitePickaxe.Client.UI.Game
{
    public sealed class ShopProductCardView : MonoBehaviour
    {
        [SerializeField] private Button cardButton;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private Image rarityFrameImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private Color insufficientPriceColor = new Color(0.9f, 0.2f, 0.2f, 1f);

        private uint productId;
        private Action<uint> clickHandler;
        private Color normalPriceColor;
        private bool hasPriceColor;

        private void Awake()
        {
            if (cardButton != null)
            {
                cardButton.onClick.RemoveAllListeners();
                cardButton.onClick.AddListener(() => clickHandler?.Invoke(productId));
            }

            if (priceText != null)
            {
                normalPriceColor = priceText.color;
                hasPriceColor = true;
            }
        }

        public void Bind(uint id,
            string title,
            Sprite icon,
            Color frameColor,
            ulong? priceAmount,
            bool isAffordable,
            Action<uint> onClick)
        {
            productId = id;
            clickHandler = onClick;

            if (nameText != null)
            {
                nameText.text = string.IsNullOrEmpty(title) ? $"PRODUCT {id}" : title;
            }

            if (rarityFrameImage != null)
            {
                rarityFrameImage.color = frameColor;
            }

            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }

            if (priceText != null)
            {
                if (priceAmount.HasValue)
                {
                    priceText.text = priceAmount.Value.ToString("N0");
                }
                else
                {
                    priceText.text = "-";
                }

                if (!hasPriceColor)
                {
                    normalPriceColor = priceText.color;
                    hasPriceColor = true;
                }

                priceText.color = priceAmount.HasValue && !isAffordable
                    ? insufficientPriceColor
                    : normalPriceColor;
            }
        }
    }
}
