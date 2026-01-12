using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Infinitepickaxe;

namespace InfinitePickaxe.Client.UI.Game
{
    public sealed class MailRewardItemView : MonoBehaviour
    {
        public enum MailRewardRarity
        {
            Common,
            Rare,
            Epic,
            Legendary
        }

        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI amountText;
        [SerializeField] private Sprite goldSprite;
        [SerializeField] private Sprite crystalSprite;
        [SerializeField] private Sprite itemSprite;
        [SerializeField] private Color commonColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        [SerializeField] private Color rareColor = new Color(0.2f, 0.55f, 0.3f, 0.9f);
        [SerializeField] private Color epicColor = new Color(0.25f, 0.35f, 0.75f, 0.9f);
        [SerializeField] private Color legendaryColor = new Color(0.9f, 0.75f, 0.2f, 0.9f);

        public void Apply(RewardType rewardType, string rewardKey, ulong amount, MailRewardRarity rarity)
        {
            EnsureReferences();

            if (amountText != null)
            {
                amountText.text = amount.ToString("N0");
            }

            if (iconImage != null)
            {
                iconImage.sprite = ResolveSprite(rewardType);
                iconImage.color = iconImage.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.6f);
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = ResolveRarityColor(rarity);
            }
        }

        private void EnsureReferences()
        {
            if (backgroundImage == null)
            {
                backgroundImage = GetComponent<Image>();
            }

            if (iconImage == null)
            {
                var tf = FindChildRecursive(transform, "Icon");
                if (tf != null) iconImage = tf.GetComponent<Image>();
            }

            if (amountText == null)
            {
                var tf = FindChildRecursive(transform, "AmountText");
                if (tf != null) amountText = tf.GetComponent<TextMeshProUGUI>();
            }
        }

        private Sprite ResolveSprite(RewardType rewardType)
        {
            switch (rewardType)
            {
                case RewardType.Gold:
                    return goldSprite;
                case RewardType.Crystal:
                    return crystalSprite;
                case RewardType.Item:
                    return itemSprite != null ? itemSprite : goldSprite;
                default:
                    return itemSprite;
            }
        }

        private Color ResolveRarityColor(MailRewardRarity rarity)
        {
            switch (rarity)
            {
                case MailRewardRarity.Rare:
                    return rareColor;
                case MailRewardRarity.Epic:
                    return epicColor;
                case MailRewardRarity.Legendary:
                    return legendaryColor;
                default:
                    return commonColor;
            }
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
    }
}
