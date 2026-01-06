using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InfinitePickaxe.Client.UI.Game
{
    public sealed class WeeklyRewardModalController : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private GameObject rewardCrystalContainer;
        [SerializeField] private TextMeshProUGUI rewardCrystalText;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backgroundButton;

        private void Awake()
        {
            EnsureReferences();
            BindButtons();
        }

        public void Show(uint rewardCrystal)
        {
            EnsureReferences();

            if (rewardCrystalContainer != null)
            {
                rewardCrystalContainer.SetActive(rewardCrystal > 0);
            }

            if (rewardCrystalText != null)
            {
                rewardCrystalText.text = rewardCrystal > 0 ? rewardCrystal.ToString("N0") : string.Empty;
            }

            if (titleText != null)
            {
                titleText.text = "보상 수령";
            }

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void BindButtons()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Hide);
            }

            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveAllListeners();
                backgroundButton.onClick.AddListener(Hide);
            }
        }

        private void EnsureReferences()
        {
            if (titleText == null)
            {
                titleText = FindText("ModalPanel/TitleText", "TitleText");
            }

            if (rewardCrystalContainer == null)
            {
                var tf = transform.Find("ModalPanel/RewardCrystalContainer");
                if (tf != null) rewardCrystalContainer = tf.gameObject;
            }

            if (rewardCrystalText == null)
            {
                rewardCrystalText = FindText("ModalPanel/RewardCrystalContainer/RewardCrystalText", "RewardCrystalText");
            }

            if (closeButton == null)
            {
                closeButton = FindButton("ModalPanel/CloseButton", "CloseButton");
            }

            if (backgroundButton == null)
            {
                backgroundButton = GetComponent<Button>();
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
