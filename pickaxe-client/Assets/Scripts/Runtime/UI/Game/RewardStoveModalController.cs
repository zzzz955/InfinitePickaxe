using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InfinitePickaxe.Client.UI.Game
{
    public sealed class RewardStoveModalController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private GameObject rewardCrystalContainer;
        [SerializeField] private TextMeshProUGUI rewardCrystalText;
        [SerializeField] private GameObject rewardGoldContainer;
        [SerializeField] private TextMeshProUGUI rewardGoldText;
        [SerializeField] private TextMeshProUGUI claimText;
        [SerializeField] private Button backgroundButton;
        [SerializeField] private float claimBlinkInterval = 1f;
        [SerializeField] private float claimBlinkMinAlpha = 0.35f;
        [SerializeField] private float fadeOutDuration = 0.5f;
        [SerializeField] private bool closeOnAnyInput = true;

        private Coroutine fadeRoutine;
        private Coroutine blinkRoutine;
        private Color claimTextBaseColor;
        private bool hasClaimTextBaseColor;

        private void Awake()
        {
            EnsureReferences();
            BindButtons();
        }

        private void OnDisable()
        {
            StopBlink();
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }
        }

        private void Update()
        {
            if (!closeOnAnyInput) return;
            if (!gameObject.activeInHierarchy) return;
            if (fadeRoutine != null) return;
            if (HasPointerDown())
            {
                Close();
            }
        }

        public void Show(uint rewardCrystal, ulong rewardGold)
        {
            EnsureReferences();

            if (rewardCrystal == 0 && rewardGold == 0) return;

            ApplyReward(rewardCrystal, rewardGold);

            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }

            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
            StartBlink();
        }

        public void Close()
        {
            if (!gameObject.activeInHierarchy) return;

            StopBlink();

            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
            }

            fadeRoutine = StartCoroutine(FadeOutRoutine());
        }

        private IEnumerator FadeOutRoutine()
        {
            if (canvasGroup == null)
            {
                gameObject.SetActive(false);
                fadeRoutine = null;
                yield break;
            }

            float duration = Mathf.Max(0f, fadeOutDuration);
            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            if (duration <= 0f)
            {
                canvasGroup.alpha = 0f;
                gameObject.SetActive(false);
                fadeRoutine = null;
                yield break;
            }

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                yield return null;
            }

            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
            fadeRoutine = null;
        }

        private void StartBlink()
        {
            if (claimText == null) return;
            StopBlink();
            CacheClaimTextColor();
            float interval = Mathf.Max(0.1f, claimBlinkInterval);
            blinkRoutine = StartCoroutine(BlinkClaimTextRoutine(interval));
        }

        private void StopBlink()
        {
            if (blinkRoutine != null)
            {
                StopCoroutine(blinkRoutine);
                blinkRoutine = null;
            }

            if (claimText != null)
            {
                if (hasClaimTextBaseColor)
                {
                    var color = claimTextBaseColor;
                    claimText.color = color;
                }
            }
        }

        private IEnumerator BlinkClaimTextRoutine(float interval)
        {
            float half = interval * 0.5f;
            float maxAlpha = claimTextBaseColor.a;
            float minAlpha = Mathf.Clamp01(claimBlinkMinAlpha) * maxAlpha;
            bool visible = true;

            while (true)
            {
                SetClaimTextAlpha(visible ? maxAlpha : minAlpha);
                visible = !visible;
                yield return new WaitForSecondsRealtime(half);
            }
        }

        private void SetClaimTextAlpha(float alpha)
        {
            if (claimText == null) return;
            var color = claimTextBaseColor;
            color.a = Mathf.Clamp01(alpha);
            claimText.color = color;
        }

        private void CacheClaimTextColor()
        {
            if (claimText == null) return;
            claimTextBaseColor = claimText.color;
            hasClaimTextBaseColor = true;
        }

        private void ApplyReward(uint rewardCrystal, ulong rewardGold)
        {
            if (rewardCrystalContainer != null)
            {
                rewardCrystalContainer.SetActive(rewardCrystal > 0);
            }

            if (rewardCrystalText != null)
            {
                rewardCrystalText.text = rewardCrystal > 0 ? rewardCrystal.ToString("N0") : string.Empty;
            }

            if (rewardGoldContainer != null)
            {
                rewardGoldContainer.SetActive(rewardGold > 0);
            }

            if (rewardGoldText != null)
            {
                rewardGoldText.text = rewardGold > 0 ? rewardGold.ToString("N0") : string.Empty;
            }
        }

        private bool HasPointerDown()
        {
            if (Input.GetMouseButtonDown(0)) return true;
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                return touch.phase == TouchPhase.Began;
            }
            return false;
        }

        private void BindButtons()
        {
            if (backgroundButton == null) return;
            backgroundButton.onClick.RemoveAllListeners();
            backgroundButton.onClick.AddListener(Close);
        }

        private void EnsureReferences()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (rewardCrystalContainer == null)
            {
                var tf = FindChildRecursive(transform, "RewardCrystalContainer");
                if (tf != null) rewardCrystalContainer = tf.gameObject;
            }

            if (rewardGoldContainer == null)
            {
                var tf = FindChildRecursive(transform, "RewardGoldContainer");
                if (tf != null) rewardGoldContainer = tf.gameObject;
            }

            if (rewardCrystalText == null)
            {
                rewardCrystalText = FindText("RewardCrystalText");
            }

            if (rewardGoldText == null)
            {
                rewardGoldText = FindText("RewardGoldText");
            }

            if (claimText == null)
            {
                claimText = FindText("ClaimText");
            }

            if (backgroundButton == null)
            {
                var tf = FindChildRecursive(transform, "Background");
                if (tf != null)
                {
                    backgroundButton = tf.GetComponent<Button>();
                }

                if (backgroundButton == null)
                {
                    backgroundButton = GetComponent<Button>();
                }
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
                if (child.name == name) return child;
                var found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
