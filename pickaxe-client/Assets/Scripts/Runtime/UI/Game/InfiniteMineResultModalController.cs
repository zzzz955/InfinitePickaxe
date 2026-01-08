using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using InfinitePickaxe.Client.Net;
using Infinitepickaxe;

namespace InfinitePickaxe.Client.UI.Game
{
    public sealed class InfiniteMineResultModalController : MonoBehaviour
    {
        [Header("Result Texts")]
        [SerializeField] private TextMeshProUGUI resultTitleText;
        [SerializeField] private TextMeshProUGUI currentFloorText;
        [SerializeField] private TextMeshProUGUI nextFloorText;
        [SerializeField] private TextMeshProUGUI rewardTitleText;
        [SerializeField] private TextMeshProUGUI actionLabelText;
        [SerializeField] private TextMeshProUGUI countdownText;

        [Header("Rewards")]
        [SerializeField] private GameObject rewardGoldContainer;
        [SerializeField] private TextMeshProUGUI rewardGoldText;
        [SerializeField] private GameObject rewardCrystalContainer;
        [SerializeField] private TextMeshProUGUI rewardCrystalText;

        [Header("Buttons")]
        [SerializeField] private Button actionButton;
        [SerializeField] private TextMeshProUGUI actionButtonText;
        [SerializeField] private Button exitButton;

        [Header("Auto Next")]
        [SerializeField] private float autoNextDelaySeconds = 5f;
        [SerializeField] private float blinkIntervalSeconds = 1f;

        private InfiniteMineSimulationViewController simulationView;
        private MessageHandler messageHandler;
        private Coroutine autoNextRoutine;
        private bool lastSuccess;
        private uint currentFloor;
        private uint nextFloor;

        private const string ClearSuccessText = "\uD074\uB9AC\uC5B4 \uC131\uACF5";
        private const string ClearFailText = "\uD074\uB9AC\uC5B4 \uC2E4\uD328";
        private const string CurrentFloorFormat = "\uD604\uC7AC \uC2A4\uD14C\uC774\uC9C0 : \uC9C0\uD558 {0}\uCE35";
        private const string NextFloorFormat = "\uB2E4\uC74C \uC2A4\uD14C\uC774\uC9C0 : \uC9C0\uD558 {0}\uCE35";
        private const string RewardSuccessText = "\uC544\uB798 \uBCF4\uC0C1 \uC218\uB839";
        private const string RewardFailText = "\uBCF4\uC0C1 \uD68D\uB4DD \uC2E4\uD328";
        private const string ActionNextText = "\uB2E4\uC74C \uC2A4\uD14C\uC774\uC9C0 \uB3C4\uC804";
        private const string ActionRetryText = "\uD604\uC7AC \uC2A4\uD14C\uC774\uC9C0 \uC7AC\uB3C4\uC804";
        private const string CountdownFormat = "{0}\uCD08 \uB4A4 \uB2E4\uC74C \uC2A4\uD14C\uC774\uC9C0\uC5D0 \uB3C4\uC804\uD569\uB2C8\uB2E4";

        private void Awake()
        {
            EnsureReferences();
            BindButtons();
        }

        private void OnDisable()
        {
            StopAutoNext();
        }

        public void SetSimulationView(InfiniteMineSimulationViewController view)
        {
            simulationView = view;
        }

        public void Show(InfiniteMineChallengeResult result)
        {
            if (result == null) return;
            EnsureReferences();
            BindButtons();

            lastSuccess = result.Success;
            currentFloor = result.Floor;
            nextFloor = currentFloor < uint.MaxValue ? currentFloor + 1 : currentFloor;

            ApplyResult(result);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            if (lastSuccess)
            {
                StartAutoNext();
            }
            else
            {
                StopAutoNext();
            }
        }

        public void Hide()
        {
            StopAutoNext();
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void ApplyResult(InfiniteMineChallengeResult result)
        {
            if (resultTitleText != null)
            {
                resultTitleText.text = lastSuccess ? ClearSuccessText : ClearFailText;
            }

            if (currentFloorText != null)
            {
                currentFloorText.text = string.Format(CurrentFloorFormat, currentFloor);
            }

            if (nextFloorText != null)
            {
                nextFloorText.gameObject.SetActive(lastSuccess);
                if (lastSuccess)
                {
                    nextFloorText.text = string.Format(NextFloorFormat, nextFloor);
                }
            }

            if (rewardTitleText != null)
            {
                rewardTitleText.text = lastSuccess ? RewardSuccessText : RewardFailText;
            }

            if (rewardGoldContainer != null)
            {
                rewardGoldContainer.SetActive(lastSuccess);
            }

            if (rewardCrystalContainer != null)
            {
                rewardCrystalContainer.SetActive(lastSuccess);
            }

            if (rewardGoldText != null)
            {
                rewardGoldText.text = lastSuccess ? result.RewardGold.ToString("N0") : string.Empty;
            }

            if (rewardCrystalText != null)
            {
                rewardCrystalText.text = lastSuccess ? result.RewardCrystal.ToString("N0") : string.Empty;
            }

            string actionText = lastSuccess ? ActionNextText : ActionRetryText;
            if (actionLabelText != null)
            {
                actionLabelText.text = actionText;
            }
            if (actionButtonText != null)
            {
                actionButtonText.text = actionText;
            }

            if (countdownText != null)
            {
                countdownText.gameObject.SetActive(lastSuccess);
                if (lastSuccess)
                {
                    countdownText.text = string.Format(CountdownFormat, Mathf.RoundToInt(autoNextDelaySeconds));
                }
            }
        }

        private void BindButtons()
        {
            if (actionButton != null)
            {
                actionButton.onClick.RemoveAllListeners();
                actionButton.onClick.AddListener(OnActionClicked);
            }

            if (exitButton != null)
            {
                exitButton.onClick.RemoveAllListeners();
                exitButton.onClick.AddListener(OnExitClicked);
            }
        }

        private void OnActionClicked()
        {
            StopAutoNext();
            uint targetFloor = lastSuccess ? nextFloor : currentFloor;
            RequestChallengeStart(targetFloor);
            Hide();
        }

        private void OnExitClicked()
        {
            StopAutoNext();
            messageHandler ??= MessageHandler.Instance;
            messageHandler?.RequestInfiniteMineExit();
            Hide();
            if (simulationView != null)
            {
                simulationView.Hide();
            }
        }

        private void RequestChallengeStart(uint floor)
        {
            messageHandler ??= MessageHandler.Instance;
            messageHandler?.RequestInfiniteMineChallengeStart(floor);
        }

        private void StartAutoNext()
        {
            StopAutoNext();
            if (!lastSuccess) return;
            if (autoNextDelaySeconds <= 0f)
            {
                OnActionClicked();
                return;
            }

            autoNextRoutine = StartCoroutine(AutoNextRoutine());
        }

        private void StopAutoNext()
        {
            if (autoNextRoutine != null)
            {
                StopCoroutine(autoNextRoutine);
                autoNextRoutine = null;
            }

            if (countdownText != null && !lastSuccess)
            {
                countdownText.gameObject.SetActive(false);
            }
        }

        private IEnumerator AutoNextRoutine()
        {
            int totalSeconds = Mathf.Max(1, Mathf.RoundToInt(autoNextDelaySeconds));
            bool visible = true;

            for (int remaining = totalSeconds; remaining > 0; remaining--)
            {
                UpdateCountdownLabel(remaining);
                if (countdownText != null)
                {
                    countdownText.gameObject.SetActive(visible);
                }
                visible = !visible;
                yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, blinkIntervalSeconds));
            }

            if (!lastSuccess) yield break;
            OnActionClicked();
        }

        private void UpdateCountdownLabel(int seconds)
        {
            if (countdownText == null) return;
            countdownText.text = string.Format(CountdownFormat, seconds);
        }

        private void EnsureReferences()
        {
            if (resultTitleText == null)
            {
                resultTitleText = FindText("ResultTitleText", "TitleText");
            }

            if (currentFloorText == null)
            {
                currentFloorText = FindText("CurrentFloorText", "CurrentFloorText");
            }

            if (nextFloorText == null)
            {
                nextFloorText = FindText("NextFloorText", "NextFloorText");
            }

            if (rewardTitleText == null)
            {
                rewardTitleText = FindText("RewardTitleText", "RewardTitleText");
            }

            if (actionLabelText == null)
            {
                actionLabelText = FindText("ActionLabelText", "ActionLabelText");
            }

            if (countdownText == null)
            {
                countdownText = FindText("CountdownText", "CountdownText");
            }

            if (rewardGoldContainer == null)
            {
                var tf = FindChildRecursive(transform, "RewardGoldContainer");
                if (tf != null) rewardGoldContainer = tf.gameObject;
            }

            if (rewardGoldText == null)
            {
                rewardGoldText = FindText("RewardGoldText", "RewardGoldText");
            }

            if (rewardCrystalContainer == null)
            {
                var tf = FindChildRecursive(transform, "RewardCrystalContainer");
                if (tf != null) rewardCrystalContainer = tf.gameObject;
            }

            if (rewardCrystalText == null)
            {
                rewardCrystalText = FindText("RewardCrystalText", "RewardCrystalText");
            }

            if (actionButton == null)
            {
                actionButton = FindButton("ActionButton", "ActionButton");
            }

            if (actionButtonText == null && actionButton != null)
            {
                actionButtonText = actionButton.GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (exitButton == null)
            {
                exitButton = FindButton("ExitButton", "ExitButton");
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

        private Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name.Equals(name))
                    return child;
                var found = FindChildRecursive(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
