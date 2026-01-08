using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InfinitePickaxe.Client.UI.Game
{
    public sealed class InfiniteMineStageCardView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI floorText;
        [SerializeField] private GameObject lockedOverlay;
        [SerializeField] private GameObject clearedMarker;
        [SerializeField] private GameObject currentMarker;
        [SerializeField] private Button actionButton;
        [SerializeField] private TextMeshProUGUI actionButtonText;
        [SerializeField] private GameObject rewardGoldContainer;
        [SerializeField] private TextMeshProUGUI rewardGoldText;
        [SerializeField] private GameObject rewardCrystalContainer;
        [SerializeField] private TextMeshProUGUI rewardCrystalText;
        [SerializeField] private float disabledAlpha = 0.5f;

        private readonly Dictionary<Graphic, Color> actionButtonGraphics = new Dictionary<Graphic, Color>();

        public void Apply(InfiniteMineStageCardData data, Action<uint> onChallenge, Action<uint> onAutoClaim)
        {
            EnsureReferences();

            if (floorText != null)
            {
                floorText.text = data.Floor.ToString("N0");
            }

            if (lockedOverlay != null)
            {
                lockedOverlay.SetActive(data.IsLocked);
            }

            if (clearedMarker != null)
            {
                clearedMarker.SetActive(data.IsCleared);
            }

            if (currentMarker != null)
            {
                currentMarker.SetActive(data.IsCurrent);
            }

            if (rewardGoldContainer != null)
            {
                rewardGoldContainer.SetActive(data.RewardGold > 0);
            }

            if (rewardGoldText != null)
            {
                rewardGoldText.text = data.RewardGold > 0 ? data.RewardGold.ToString("N0") : string.Empty;
            }

            if (rewardCrystalContainer != null)
            {
                rewardCrystalContainer.SetActive(data.RewardCrystal > 0);
            }

            if (rewardCrystalText != null)
            {
                rewardCrystalText.text = data.RewardCrystal > 0 ? data.RewardCrystal.ToString("N0") : string.Empty;
            }

            ApplyActionButton(data, onChallenge, onAutoClaim);
        }

        private void ApplyActionButton(InfiniteMineStageCardData data, Action<uint> onChallenge, Action<uint> onAutoClaim)
        {
            if (actionButton == null) return;

            actionButton.onClick.RemoveAllListeners();

            bool interactable = false;
            string label = string.Empty;

            if (!data.IsCleared)
            {
                if (data.CanChallenge)
                {
                    interactable = true;
                    label = "도  전";
                    if (onChallenge != null)
                    {
                        uint floor = data.Floor;
                        actionButton.onClick.AddListener(() => onChallenge(floor));
                    }
                }
                else
                {
                    label = "잠  김";
                }
            }
            else
            {
                if (data.CanAutoClaim)
                {
                    interactable = true;
                    label = "자동채굴";
                    if (onAutoClaim != null)
                    {
                        uint floor = data.Floor;
                        actionButton.onClick.AddListener(() => onAutoClaim(floor));
                    }
                }
                else
                {
                    label = data.AutoClaimedToday ? "수령완료" : "자동채굴";
                }
            }

            actionButton.interactable = interactable;
            CacheButtonGraphics(actionButton, actionButtonGraphics);
            ApplyButtonGraphics(actionButton, interactable, disabledAlpha, actionButtonGraphics);

            if (actionButtonText != null)
            {
                actionButtonText.text = label;
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

        private void ApplyButtonGraphics(Button button, bool isEnabled, float disabledAlphaValue, Dictionary<Graphic, Color> cache)
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
                    graphic.color = new Color(gray, gray, gray, baseColor.a * disabledAlphaValue);
                }
            }
        }

        private void EnsureReferences()
        {
            if (floorText == null)
            {
                floorText = FindText("FloorText", "FloorText");
            }

            if (lockedOverlay == null)
            {
                var tf = transform.Find("LockedOverlay");
                if (tf != null) lockedOverlay = tf.gameObject;
            }

            if (clearedMarker == null)
            {
                var tf = transform.Find("ClearedMarker");
                if (tf != null) clearedMarker = tf.gameObject;
            }

            if (currentMarker == null)
            {
                var tf = transform.Find("CurrentMarker");
                if (tf != null) currentMarker = tf.gameObject;
            }

            if (actionButton == null)
            {
                actionButton = FindButton("ActionButton", "ActionButton");
                if (actionButton == null)
                {
                    actionButton = FindButton("ChallengeButton", "ChallengeButton");
                }
                if (actionButton == null)
                {
                    actionButton = FindButton("AutoClaimButton", "AutoClaimButton");
                }
            }

            if (actionButtonText == null && actionButton != null)
            {
                actionButtonText = actionButton.GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (rewardGoldContainer == null)
            {
                var tf = transform.Find("RewardGoldContainer");
                if (tf != null) rewardGoldContainer = tf.gameObject;
            }

            if (rewardGoldText == null)
            {
                rewardGoldText = FindText("RewardGoldText", "RewardGoldText");
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

    public struct InfiniteMineStageCardData
    {
        public uint Floor;
        public bool IsCleared;
        public bool IsCurrent;
        public bool IsLocked;
        public bool CanChallenge;
        public bool CanAutoClaim;
        public bool AutoClaimedToday;
        public ulong RewardGold;
        public uint RewardCrystal;
    }
}
