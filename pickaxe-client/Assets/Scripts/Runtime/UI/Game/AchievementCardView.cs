using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InfinitePickaxe.Client.UI.Game
{
    public class AchievementCardView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TextMeshProUGUI targetText;
        [SerializeField] private Button claimButton;
        [SerializeField] private TextMeshProUGUI claimButtonText;

        [Header("Reward - Crystal")]
        [SerializeField] private GameObject rewardCrystalContainer;
        [SerializeField] private TextMeshProUGUI rewardCrystalText;

        [Header("Reward - Gold")]
        [SerializeField] private GameObject rewardGoldContainer;
        [SerializeField] private TextMeshProUGUI rewardGoldText;

        [SerializeField] private float claimButtonDisabledAlpha = 0.5f;
        private readonly Dictionary<Graphic, Color> claimButtonGraphicColors = new Dictionary<Graphic, Color>();

        public void Apply(AchievementCardData data, System.Action onClaim)
        {
            if (titleText != null)
            {
                titleText.text = data.Title;
            }

            if (descriptionText != null)
            {
                descriptionText.text = data.Description;
            }

            if (progressText != null)
            {
                progressText.text = data.ProgressText;
            }

            if (targetText != null)
            {
                targetText.text = data.TargetText ?? string.Empty;
                targetText.gameObject.SetActive(!string.IsNullOrEmpty(data.TargetText));
            }

            if (rewardCrystalContainer != null)
            {
                rewardCrystalContainer.SetActive(data.RewardCrystal > 0);
            }

            if (rewardCrystalText != null)
            {
                rewardCrystalText.text = data.RewardCrystal > 0 ? data.RewardCrystal.ToString("N0") : string.Empty;
            }

            if (rewardGoldContainer != null)
            {
                rewardGoldContainer.SetActive(data.RewardGold > 0);
            }

            if (rewardGoldText != null)
            {
                rewardGoldText.text = data.RewardGold > 0 ? data.RewardGold.ToString("N0") : string.Empty;
            }

            if (claimButton != null)
            {
                CacheButtonGraphics(claimButton, claimButtonGraphicColors);
                claimButton.onClick.RemoveAllListeners();
                claimButton.interactable = data.CanClaim;
                if (data.CanClaim && onClaim != null)
                {
                    claimButton.onClick.AddListener(() => onClaim());
                }
            }

            ApplyButtonGraphics(claimButton, data.CanClaim, claimButtonDisabledAlpha, claimButtonGraphicColors);
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
    }

    public struct AchievementCardData
    {
        public uint AchievementId;
        public uint ChainId;
        public bool CanClaim;
        public string Title;
        public string Description;
        public string ProgressText;
        public string TargetText;
        public uint RewardCrystal;
        public string RewardCrystalText;
        public ulong RewardGold;
        public string RewardGoldText;
    }
}
