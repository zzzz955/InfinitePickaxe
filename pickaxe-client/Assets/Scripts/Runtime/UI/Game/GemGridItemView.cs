using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InfinitePickaxe.Client.UI.Game
{
    public sealed class GemGridItemView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI tierText;
        [SerializeField] private TextMeshProUGUI badgeText;
        [SerializeField] private GameObject emptyState;
        [SerializeField] private GameObject filledState;
        [SerializeField] private Color normalColor = new Color(0.18f, 0.18f, 0.18f, 0.9f);
        [SerializeField] private Color emptyColor = new Color(0.08f, 0.08f, 0.08f, 0.5f);
        [SerializeField] private Color baseColor = new Color(0.3f, 0.45f, 0.75f, 0.9f);
        [SerializeField] private Color materialColor = new Color(0.3f, 0.7f, 0.45f, 0.9f);
        [SerializeField] private Color convertColor = new Color(0.7f, 0.5f, 0.25f, 0.9f);

        private int boundIndex;
        private Action<int> clickHandler;
        private bool hasData;

        public void Bind(int index, Action<int> onClick)
        {
            boundIndex = index;
            clickHandler = onClick;

            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => clickHandler?.Invoke(boundIndex));
            }
        }

        public void SetEmpty()
        {
            hasData = false;
            if (emptyState != null) emptyState.SetActive(true);
            if (filledState != null) filledState.SetActive(false);

            if (button != null) button.interactable = false;
            if (nameText != null) nameText.text = string.Empty;
            if (tierText != null) tierText.text = string.Empty;
            if (badgeText != null) badgeText.text = string.Empty;

            ApplyBackgroundColor(emptyColor);
        }

        public void SetData(string displayName, string tierLabel, Sprite icon)
        {
            hasData = true;
            if (emptyState != null) emptyState.SetActive(false);
            if (filledState != null) filledState.SetActive(true);

            if (button != null) button.interactable = true;
            if (nameText != null) nameText.text = displayName;
            if (tierText != null) tierText.text = tierLabel;
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.color = icon != null ? Color.white : new Color(1f, 1f, 1f, 0.6f);
            }

            ApplyBackgroundColor(normalColor);
        }

        public void SetSelectionRole(GemSelectionRole role)
        {
            if (!hasData)
            {
                if (badgeText != null) badgeText.text = string.Empty;
                ApplyBackgroundColor(emptyColor);
                return;
            }

            if (badgeText == null && backgroundImage == null) return;

            switch (role)
            {
                case GemSelectionRole.Base:
                    if (badgeText != null) badgeText.text = "기준";
                    ApplyBackgroundColor(baseColor);
                    break;
                case GemSelectionRole.Material:
                    if (badgeText != null) badgeText.text = "재료";
                    ApplyBackgroundColor(materialColor);
                    break;
                case GemSelectionRole.Convert:
                    if (badgeText != null) badgeText.text = "전환";
                    ApplyBackgroundColor(convertColor);
                    break;
                default:
                    if (badgeText != null) badgeText.text = string.Empty;
                    ApplyBackgroundColor(normalColor);
                    break;
            }
        }

        private void ApplyBackgroundColor(Color color)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = color;
            }
        }
    }
}
