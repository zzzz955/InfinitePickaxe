using System;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InfinitePickaxe.Client.UI.Game
{
    public sealed class WeeklyMissionCardView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button claimButton;
        [SerializeField] private TextMeshProUGUI claimButtonText;
        [SerializeField] private GameObject rewardCrystalContainer;
        [SerializeField] private TextMeshProUGUI rewardCrystalText;
        [SerializeField] private Color activeStatusColor = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color completedStatusColor = new Color(0.2f, 0.85f, 0.3f, 1f);
        [SerializeField] private Color claimedStatusColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        [SerializeField] private float claimButtonDisabledAlpha = 0.5f;
        private readonly Dictionary<Graphic, Color> claimButtonGraphicColors = new Dictionary<Graphic, Color>();

        public void Apply(
            string title,
            string description,
            string progress,
            uint rewardCrystal,
            string status,
            QuestMissionItemView.MissionStatusState statusState,
            bool canClaim,
            Action onClaim)
        {
            EnsureReferences();

            if (titleText != null)
            {
                titleText.text = title ?? string.Empty;
            }

            if (descriptionText != null)
            {
                descriptionText.text = description ?? string.Empty;
            }

            if (progressText != null)
            {
                progressText.text = progress ?? string.Empty;
            }

            if (statusText != null)
            {
                statusText.text = status ?? string.Empty;
                statusText.color = ResolveStatusColor(statusState);
            }

            if (rewardCrystalContainer != null)
            {
                rewardCrystalContainer.SetActive(rewardCrystal > 0);
            }

            if (rewardCrystalText != null)
            {
                rewardCrystalText.text = rewardCrystal > 0 ? rewardCrystal.ToString("N0") : string.Empty;
            }

            if (claimButton != null)
            {
                CacheButtonGraphics(claimButton, claimButtonGraphicColors);
                claimButton.onClick.RemoveAllListeners();
                claimButton.interactable = canClaim;
                if (canClaim && onClaim != null)
                {
                    claimButton.onClick.AddListener(() => onClaim());
                }
            }

            UpdateClaimButtonText(statusState, canClaim);
            ApplyButtonGraphics(claimButton, canClaim, claimButtonDisabledAlpha, claimButtonGraphicColors);
        }

        private void UpdateClaimButtonText(QuestMissionItemView.MissionStatusState statusState, bool canClaim)
        {
            if (claimButtonText == null) return;

            if (canClaim)
            {
                claimButtonText.text = "보상 받기";
                return;
            }

            switch (statusState)
            {
                case QuestMissionItemView.MissionStatusState.Claimed:
                    claimButtonText.text = "수령 완료";
                    break;
                case QuestMissionItemView.MissionStatusState.Completed:
                    claimButtonText.text = "보상 받기";
                    break;
                case QuestMissionItemView.MissionStatusState.Active:
                    claimButtonText.text = "진행 중";
                    break;
                default:
                    claimButtonText.text = "-";
                    break;
            }
        }

        private Color ResolveStatusColor(QuestMissionItemView.MissionStatusState statusState)
        {
            switch (statusState)
            {
                case QuestMissionItemView.MissionStatusState.Active:
                    return activeStatusColor;
                case QuestMissionItemView.MissionStatusState.Completed:
                    return completedStatusColor;
                case QuestMissionItemView.MissionStatusState.Claimed:
                    return claimedStatusColor;
                default:
                    return statusText != null ? statusText.color : Color.white;
            }
        }

        private void EnsureReferences()
        {
            if (titleText == null)
            {
                titleText = FindText("TitleText", "TitleText");
            }

            if (descriptionText == null)
            {
                descriptionText = FindText("DescriptionText", "DescriptionText");
            }

            if (progressText == null)
            {
                progressText = FindText("ProgressText", "ProgressText");
            }

            if (statusText == null)
            {
                statusText = FindText("StatusText", "StatusText");
            }

            if (claimButton == null)
            {
                claimButton = FindButton("ClaimButton", "ClaimButton");
            }

            if (claimButtonText == null && claimButton != null)
            {
                claimButtonText = claimButton.GetComponentInChildren<TextMeshProUGUI>();
            }

            if (rewardCrystalContainer == null)
            {
                var tf = transform.Find("RewardCrystalContainer");
                if (tf != null) rewardCrystalContainer = tf.gameObject;
            }

            if (rewardCrystalText == null)
            {
                rewardCrystalText = FindText("RewardCrystalText", "RewardCrystalText");
            }

        }

        private void CacheButtonGraphics(Button button, Dictionary<Graphic, Color> cache)
        {
            if (button == null) return;
            var graphics = button.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                var graphic = graphics[i];
                if (graphic == null) continue;
                if (!cache.ContainsKey(graphic))
                {
                    cache[graphic] = graphic.color;
                }
            }
        }

        private void ApplyButtonGraphics(Button button, bool isEnabled, float disabledAlpha, Dictionary<Graphic, Color> cache)
        {
            if (button == null) return;
            var graphics = button.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                var graphic = graphics[i];
                if (graphic == null) continue;
                if (!cache.TryGetValue(graphic, out var baseColor))
                {
                    baseColor = graphic.color;
                    cache[graphic] = baseColor;
                }

                if (isEnabled)
                {
                    graphic.color = baseColor;
                }
                else
                {
                    float gray = (baseColor.r + baseColor.g + baseColor.b) / 3f;
                    graphic.color = new Color(gray, gray, gray, baseColor.a * disabledAlpha);
                }
            }
        }

        private TextMeshProUGUI FindText(string path, string fallbackName)
        {
            var target = transform.Find(path);
            if (target != null)
            {
                var text = target.GetComponent<TextMeshProUGUI>();
                if (text != null) return text;
            }

            if (string.IsNullOrEmpty(fallbackName)) return null;
            var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].name == fallbackName)
                {
                    return texts[i];
                }
            }

            return null;
        }

        private Button FindButton(string path, string fallbackName)
        {
            var target = transform.Find(path);
            if (target != null)
            {
                var button = target.GetComponent<Button>();
                if (button != null) return button;
            }

            if (string.IsNullOrEmpty(fallbackName)) return null;
            var buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].name == fallbackName)
                {
                    return buttons[i];
                }
            }

            return null;
        }
    }
}
