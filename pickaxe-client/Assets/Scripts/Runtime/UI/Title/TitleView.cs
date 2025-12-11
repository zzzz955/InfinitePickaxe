using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InfinitePickaxe.Client.UI.Title
{
    public enum TitleState
    {
        Idle,
        Loading,
        Authenticated,
        Error
    }

    public sealed class TitleView : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI loadingText;
        [SerializeField] private TextMeshProUGUI errorText;
        [SerializeField] private Button googleButton;
        [SerializeField] private Button startButton;
        [SerializeField] private CanvasGroup overlayGroup;

        private System.Action onGoogleClicked;
        private System.Action onStartClicked;
        private TitleState state = TitleState.Idle;

        private void Awake()
        {
            if (googleButton == null)
            {
                googleButton = FindButtonByName("GoogleButton");
            }
            if (startButton == null)
            {
                startButton = FindButtonByName("StartButton");
            }
            if (startButton == null && googleButton != null)
            {
                startButton = CreateStartButtonFrom(googleButton);
            }
            if (overlayGroup == null)
            {
                overlayGroup = CreateOverlay();
            }

            ApplyState();
        }

        public void SetButtonHandlers(System.Action onGoogle, System.Action onStart)
        {
            onGoogleClicked = onGoogle;
            onStartClicked = onStart;

            if (googleButton != null)
            {
                googleButton.onClick.RemoveAllListeners();
                googleButton.onClick.AddListener(() => onGoogleClicked?.Invoke());
            }

            if (startButton != null)
            {
                startButton.onClick.RemoveAllListeners();
                startButton.onClick.AddListener(() => onStartClicked?.Invoke());
            }
        }

        public void SetState(TitleState newState, string statusMessage = null)
        {
            if (state == newState && string.IsNullOrEmpty(statusMessage))
            {
                return;
            }

            state = newState;
            if (!string.IsNullOrEmpty(statusMessage) && statusText != null)
            {
                statusText.text = statusMessage;
            }

            ApplyState();
        }

        public void SetLoadingMessage(string message)
        {
            if (loadingText != null && !string.IsNullOrEmpty(message))
            {
                loadingText.text = message;
            }
        }

        public void SetError(string errorMessage)
        {
            if (errorText != null)
            {
                errorText.text = errorMessage;
            }

            state = TitleState.Error;
            ApplyState();
        }

        public void ShowOverlay(bool show, string message = null)
        {
            if (overlayGroup == null)
            {
                return;
            }

            overlayGroup.alpha = show ? 1f : 0f;
            overlayGroup.interactable = show;
            overlayGroup.blocksRaycasts = show;

            if (!string.IsNullOrEmpty(message))
            {
                SetLoadingMessage(message);
            }
        }

        private void ApplyState()
        {
            var isLoading = state == TitleState.Loading;
            var isError = state == TitleState.Error;
            var isAuthenticated = state == TitleState.Authenticated;

            if (statusText != null)
            {
                if (string.IsNullOrEmpty(statusText.text))
                {
                    statusText.text = state switch
                    {
                        TitleState.Idle => "로그인 상태: 미인증",
                        TitleState.Loading => "로그인 상태: 진행 중...",
                        TitleState.Authenticated => "로그인 상태: 인증 완료",
                        TitleState.Error => "로그인 상태: 오류",
                        _ => statusText.text
                    };
                }
            }

            if (loadingText != null)
            {
                loadingText.gameObject.SetActive(isLoading);
            }

            if (errorText != null)
            {
                errorText.gameObject.SetActive(isError);
            }

            var buttonsInteractable = !isLoading;
            if (googleButton != null)
            {
                googleButton.gameObject.SetActive(!isAuthenticated && !isLoading);
                googleButton.interactable = buttonsInteractable && !isAuthenticated;
            }

            if (startButton != null)
            {
                startButton.gameObject.SetActive(isAuthenticated && !isLoading);
                startButton.interactable = buttonsInteractable && isAuthenticated;
            }
        }

        private Button FindButtonByName(string name)
        {
            var transforms = GetComponentsInChildren<Transform>(true);
            foreach (var tr in transforms)
            {
                if (tr.name == name && tr.TryGetComponent(out Button btn))
                {
                    return btn;
                }
            }
            return null;
        }

        private CanvasGroup CreateOverlay()
        {
            var overlay = new GameObject("LoadingOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(CanvasGroup));
            overlay.transform.SetParent(transform, false);
            var rt = overlay.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = overlay.GetComponent<UnityEngine.UI.Image>();
            img.color = new Color(0f, 0f, 0f, 0.35f);
            img.raycastTarget = true;

            var cg = overlay.GetComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;

            return cg;
        }

        private Button CreateStartButtonFrom(Button source)
        {
            var clone = Instantiate(source.gameObject, source.transform.parent);
            clone.name = "StartButton";
            var rt = clone.GetComponent<RectTransform>();
            rt.anchoredPosition += new Vector2(0, -120f);

            var text = clone.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = "게임 시작";
            }

            var btn = clone.GetComponent<Button>();
            return btn;
        }
    }
}
