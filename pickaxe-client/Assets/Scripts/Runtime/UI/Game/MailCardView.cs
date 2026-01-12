using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using InfinitePickaxe.Client.Core;

namespace InfinitePickaxe.Client.UI.Game
{
    public sealed class MailCardView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private TextMeshProUGUI senderText;
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Transform rewardContainer;
        [SerializeField] private Button claimButton;
        [SerializeField] private TextMeshProUGUI claimButtonText;
        [SerializeField] private float claimButtonDisabledAlpha = 0.5f;

        private readonly List<GameObject> rewardItems = new List<GameObject>();
        private readonly Dictionary<Graphic, Color> claimButtonGraphicColors = new Dictionary<Graphic, Color>();

        public void Apply(
            MailCardViewData data,
            GameObject rewardItemPrefab,
            Func<MailRewardEntry, MailRewardItemView.MailRewardRarity> resolveRarity,
            Action<string> onClaim)
        {
            EnsureReferences();

            if (titleText != null)
            {
                titleText.text = data.Title ?? string.Empty;
            }

            if (bodyText != null)
            {
                bodyText.text = data.Body ?? string.Empty;
            }

            if (senderText != null)
            {
                senderText.text = data.Sender ?? string.Empty;
            }

            if (timeText != null)
            {
                timeText.text = data.TimeLabel ?? string.Empty;
            }

            UpdateStatusText(data);
            UpdateClaimButton(data, onClaim);
            UpdateRewards(data, rewardItemPrefab, resolveRarity);
        }

        private void UpdateRewards(
            MailCardViewData data,
            GameObject rewardItemPrefab,
            Func<MailRewardEntry, MailRewardItemView.MailRewardRarity> resolveRarity)
        {
            ClearRewardItems();

            bool hasRewards = data.Rewards != null && data.Rewards.Count > 0;
            if (!hasRewards || rewardContainer == null || rewardItemPrefab == null)
            {
                if (rewardContainer != null)
                {
                    rewardContainer.gameObject.SetActive(false);
                }
                return;
            }

            rewardContainer.gameObject.SetActive(true);

            for (int i = 0; i < data.Rewards.Count; i++)
            {
                var reward = data.Rewards[i];
                var instance = Instantiate(rewardItemPrefab, rewardContainer, false);
                var view = instance.GetComponentInChildren<MailRewardItemView>(true);
                if (view != null)
                {
                    var rarity = resolveRarity != null
                        ? resolveRarity(reward)
                        : MailRewardItemView.MailRewardRarity.Common;
                    view.Apply(reward.RewardType, reward.RewardKey, reward.Amount, rarity);
                }
                rewardItems.Add(instance);
            }
        }

        private void UpdateClaimButton(MailCardViewData data, Action<string> onClaim)
        {
            if (claimButton == null) return;

            CacheButtonGraphics(claimButton, claimButtonGraphicColors);

            claimButton.onClick.RemoveAllListeners();
            bool canClaim = data.IsClaimable && !data.IsPending;
            claimButton.interactable = canClaim;
            if (canClaim && onClaim != null)
            {
                string mailId = data.MailId;
                claimButton.onClick.AddListener(() => onClaim(mailId));
            }

            if (claimButtonText != null)
            {
                claimButtonText.text = ResolveClaimButtonLabel(data);
            }

            ApplyButtonGraphics(claimButton, canClaim, claimButtonDisabledAlpha, claimButtonGraphicColors);
        }

        private void UpdateStatusText(MailCardViewData data)
        {
            if (statusText == null) return;

            if (data.IsClaimed)
            {
                statusText.text = "\uC218\uB839 \uC644\uB8CC";
            }
            else if (data.IsExpired)
            {
                statusText.text = "\uB9CC\uB8CC";
            }
            else if (data.HasReward)
            {
                statusText.text = "\uBBF8\uC218\uB839";
            }
            else
            {
                statusText.text = string.Empty;
            }
        }

        private string ResolveClaimButtonLabel(MailCardViewData data)
        {
            if (data.IsClaimed)
            {
                return "\uC218\uB839 \uC644\uB8CC";
            }
            if (data.IsExpired)
            {
                return "\uB9CC\uB8CC";
            }
            if (!data.HasReward)
            {
                return "\uBCF4\uC0C1 \uC5C6\uC74C";
            }
            if (data.IsPending)
            {
                return "\uC218\uB839 \uC911";
            }
            return "\uC218\uB839";
        }

        private void ClearRewardItems()
        {
            for (int i = 0; i < rewardItems.Count; i++)
            {
                if (rewardItems[i] != null)
                {
                    Destroy(rewardItems[i]);
                }
            }
            rewardItems.Clear();
        }

        private void EnsureReferences()
        {
            if (titleText == null)
            {
                titleText = FindText("TitleText");
            }

            if (bodyText == null)
            {
                bodyText = FindText("BodyText");
            }

            if (senderText == null)
            {
                senderText = FindText("SenderText");
            }

            if (timeText == null)
            {
                timeText = FindText("TimeText");
            }

            if (statusText == null)
            {
                statusText = FindText("StatusText");
            }

            if (rewardContainer == null)
            {
                var tf = FindChildRecursive(transform, "RewardContainer");
                if (tf != null) rewardContainer = tf;
            }

            if (claimButton == null)
            {
                var tf = FindChildRecursive(transform, "ClaimButton");
                if (tf != null) claimButton = tf.GetComponent<Button>();
            }

            if (claimButtonText == null && claimButton != null)
            {
                claimButtonText = claimButton.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        private TextMeshProUGUI FindText(string name)
        {
            var tf = FindChildRecursive(transform, name);
            if (tf == null) return null;
            return tf.GetComponent<TextMeshProUGUI>();
        }

        private Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }

                var found = FindChildRecursive(child, name);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
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

        public struct MailCardViewData
        {
            public string MailId;
            public string Title;
            public string Body;
            public string Sender;
            public string TimeLabel;
            public bool HasReward;
            public bool IsClaimed;
            public bool IsExpired;
            public bool IsClaimable;
            public bool IsPending;
            public IReadOnlyList<MailRewardEntry> Rewards;
        }
    }
}
