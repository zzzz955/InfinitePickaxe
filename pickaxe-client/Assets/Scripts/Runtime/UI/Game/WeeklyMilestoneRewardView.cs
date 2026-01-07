using System;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InfinitePickaxe.Client.UI.Game
{
    public sealed class WeeklyMilestoneRewardView : MonoBehaviour
    {
        [SerializeField] private uint milestoneCount;
        [SerializeField] private Button claimButton;
        [SerializeField] private TextMeshProUGUI milestoneLabelText;
        [SerializeField] private TextMeshProUGUI rewardCrystalText;
        [SerializeField] private float disabledAlpha = 0.5f;
        private readonly Dictionary<Graphic, Color> buttonGraphicColors = new Dictionary<Graphic, Color>();

        public uint MilestoneCount => milestoneCount;

        public void Apply(uint rewardCrystal, bool canClaim, bool claimed, Action<uint> onClaim)
        {
            EnsureReferences();

            if (rewardCrystalText != null)
            {
                rewardCrystalText.text = rewardCrystal.ToString("N0");
            }

            if (milestoneLabelText != null)
            {
                milestoneLabelText.text = milestoneCount.ToString();
            }

            if (claimButton != null)
            {
                CacheButtonGraphics(claimButton, buttonGraphicColors);
                claimButton.onClick.RemoveAllListeners();
                claimButton.interactable = canClaim;
                if (canClaim && onClaim != null)
                {
                    claimButton.onClick.AddListener(() => onClaim(milestoneCount));
                }
            }

            ApplyButtonGraphics(claimButton, canClaim, disabledAlpha, buttonGraphicColors);
        }

        private void EnsureReferences()
        {
            if (claimButton == null)
            {
                claimButton = FindButton("ClaimButton", "ClaimButton");
            }

            if (milestoneLabelText == null)
            {
                milestoneLabelText = FindText("MilestoneLabelText", "MilestoneLabelText");
            }

            if (milestoneLabelText == null)
            {
                milestoneLabelText = FindText("MilestoneCountText", "MilestoneCountText");
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
