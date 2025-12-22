using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InfinitePickaxe.Client.UI.Game
{
    public class QuestMissionItemView : MonoBehaviour
    {
        public enum MissionStatusState
        {
            Unknown = 0,
            Active = 1,
            Completed = 2,
            Claimed = 3
        }

        [SerializeField] private TextMeshProUGUI typeText;
        [SerializeField] private TextMeshProUGUI difficultyText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI rewardText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button claimButton;
        [SerializeField] private TextMeshProUGUI claimButtonText;
        [SerializeField] private Color activeStatusColor = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color completedStatusColor = new Color(0.2f, 0.85f, 0.3f, 1f);
        [SerializeField] private Color claimedStatusColor = new Color(0.7f, 0.7f, 0.7f, 1f);

        public void Apply(
            string type,
            string difficulty,
            string descriptionWithProgress,
            string reward,
            string status,
            MissionStatusState statusState,
            bool canClaim,
            Action onClaim)
        {
            EnsureReferences();

            if (typeText != null)
            {
                typeText.text = type;
            }

            if (difficultyText != null)
            {
                difficultyText.text = difficulty;
                difficultyText.gameObject.SetActive(!string.IsNullOrEmpty(difficulty));
            }

            if (descriptionText != null)
            {
                descriptionText.text = descriptionWithProgress;
            }

            if (rewardText != null)
            {
                rewardText.text = reward;
            }

            if (statusText != null)
            {
                statusText.text = status;
                statusText.color = ResolveStatusColor(statusState);
            }

            if (claimButton != null)
            {
                claimButton.onClick.RemoveAllListeners();
                claimButton.interactable = canClaim;
                if (onClaim != null)
                {
                    claimButton.onClick.AddListener(() => onClaim());
                }
            }

            UpdateClaimButtonText(statusState, canClaim);
        }

        private void UpdateClaimButtonText(MissionStatusState statusState, bool canClaim)
        {
            if (claimButtonText == null) return;

            if (canClaim)
            {
                claimButtonText.text = "보상 받기";
                return;
            }

            switch (statusState)
            {
                case MissionStatusState.Claimed:
                    claimButtonText.text = "수령 완료";
                    break;
                case MissionStatusState.Completed:
                    claimButtonText.text = "보상 받기";
                    break;
                case MissionStatusState.Active:
                    claimButtonText.text = "진행 중";
                    break;
                default:
                    claimButtonText.text = "-";
                    break;
            }
        }

        private Color ResolveStatusColor(MissionStatusState statusState)
        {
            switch (statusState)
            {
                case MissionStatusState.Active:
                    return activeStatusColor;
                case MissionStatusState.Completed:
                    return completedStatusColor;
                case MissionStatusState.Claimed:
                    return claimedStatusColor;
                default:
                    return statusText != null ? statusText.color : Color.white;
            }
        }

        private void EnsureReferences()
        {
            if (typeText == null)
            {
                typeText = FindText("Layout1/typeText", "typeText");
            }

            if (difficultyText == null)
            {
                difficultyText = FindText("Layout1/difficultyText", "difficultyText");
            }

            if (statusText == null)
            {
                statusText = FindText("Layout1/statusText", "statusText");
            }

            if (descriptionText == null)
            {
                descriptionText = FindText("Layout2/descriptionText", "descriptionText");
            }

            if (claimButton == null)
            {
                claimButton = FindButton("Layout2/claimButton", "claimButton");
            }

            if (rewardText == null)
            {
                rewardText = FindText("Layout2/claimButton/rewardText", "rewardText");
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
