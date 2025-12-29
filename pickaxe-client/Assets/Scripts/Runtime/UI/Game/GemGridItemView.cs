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
        [SerializeField] private Color material2Color = new Color(0.3f, 0.6f, 0.85f, 0.9f);
        [SerializeField] private Color convertColor = new Color(0.7f, 0.5f, 0.25f, 0.9f);

        private int boundIndex;
        private Action<int> clickHandler;
        private bool hasData;
        private Infinitepickaxe.GemInfo currentGemInfo;

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
            currentGemInfo = null;

            if (emptyState != null) emptyState.SetActive(true);
            if (filledState != null) filledState.SetActive(false);
            if (iconImage != null) iconImage.gameObject.SetActive(false);

            if (button != null) button.interactable = false;
            if (nameText != null) nameText.text = string.Empty;
            if (tierText != null) tierText.text = string.Empty;
            if (badgeText != null) badgeText.text = string.Empty;

            ApplyBackgroundColor(emptyColor);

            // 툴팁 트리거 제거
            RemoveTooltipTrigger();
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
                iconImage.gameObject.SetActive(true);
                iconImage.sprite = icon;
                iconImage.color = icon != null ? Color.white : new Color(1f, 1f, 1f, 0.6f);
            }

            ApplyBackgroundColor(normalColor);

            // currentGemInfo가 있으면 툴팁 트리거 추가
            if (currentGemInfo != null)
            {
                AddTooltipTrigger();
            }
        }

        /// <summary>
        /// 보석 정보와 함께 데이터 설정 (툴팁 지원)
        /// </summary>
        public void SetData(string displayName, string tierLabel, Sprite icon, Infinitepickaxe.GemInfo gemInfo)
        {
            currentGemInfo = gemInfo;
            SetData(displayName, tierLabel, icon);
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
                    if (badgeText != null) badgeText.text = "??";
                    ApplyBackgroundColor(baseColor);
                    break;
                case GemSelectionRole.Material:
                    if (badgeText != null) badgeText.text = "??";
                    ApplyBackgroundColor(materialColor);
                    break;
                case GemSelectionRole.Material2:
                    if (badgeText != null) badgeText.text = "??2";
                    ApplyBackgroundColor(material2Color);
                    break;
                case GemSelectionRole.Convert:
                    if (badgeText != null) badgeText.text = "??";
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

        /// <summary>
        /// 아이콘에 툴팁 트리거 추가
        /// </summary>
        private void AddTooltipTrigger()
        {
            if (iconImage == null || currentGemInfo == null) return;

            // 기존 트리거 제거
            var existingTrigger = iconImage.GetComponent<GemTooltipTrigger>();
            if (existingTrigger != null)
            {
                Destroy(existingTrigger);
            }

            // 새 트리거 추가
            var trigger = iconImage.gameObject.AddComponent<GemTooltipTrigger>();
            trigger.SetGemInfo(currentGemInfo);
        }

        /// <summary>
        /// 아이콘에서 툴팁 트리거 제거
        /// </summary>
        private void RemoveTooltipTrigger()
        {
            if (iconImage == null) return;

            var existingTrigger = iconImage.GetComponent<GemTooltipTrigger>();
            if (existingTrigger != null)
            {
                Destroy(existingTrigger);
            }
        }
    }
}
