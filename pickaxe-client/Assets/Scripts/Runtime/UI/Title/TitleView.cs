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
        [SerializeField] private GameObject modalPrefab;
        [SerializeField] private string modalResourcePath = "UI/Modal";
        [SerializeField] private CanvasGroup modalGroup;
        [SerializeField] private TextMeshProUGUI modalMessageText;
        [SerializeField] private Button modalConfirmButton;

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

            EnsureModalInstance();

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
            if (statusText != null)
            {
                statusText.text = "로그인 상태: 오류";
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

        public void ShowModal(string message, string buttonText = "확인", System.Action onClose = null)
        {
            EnsureModalInstance();

            if (modalMessageText != null)
            {
                modalMessageText.text = string.IsNullOrEmpty(message)
                    ? "알 수 없는 오류가 발생했습니다."
                    : message;
            }

            if (modalConfirmButton != null)
            {
                var label = modalConfirmButton.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = string.IsNullOrEmpty(buttonText) ? "확인" : buttonText;
                }

                modalConfirmButton.onClick.RemoveAllListeners();
                modalConfirmButton.onClick.AddListener(() =>
                {
                    HideModal();
                    onClose?.Invoke();
                });
            }

            modalGroup.alpha = 1f;
            modalGroup.interactable = true;
            modalGroup.blocksRaycasts = true;
            modalGroup.transform.SetAsLastSibling();
        }

        public void HideModal()
        {
            if (modalGroup == null)
            {
                return;
            }

            modalGroup.alpha = 0f;
            modalGroup.interactable = false;
            modalGroup.blocksRaycasts = false;
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

        private CanvasGroup EnsureModalInstance()
        {
            if (modalGroup != null)
            {
                return modalGroup;
            }

            GameObject instance = null;

            if (modalPrefab != null)
            {
                instance = Instantiate(modalPrefab, transform);
            }
            else if (!string.IsNullOrEmpty(modalResourcePath))
            {
                var prefab = Resources.Load<GameObject>(modalResourcePath);
                if (prefab != null)
                {
                    instance = Instantiate(prefab, transform);
                }
            }

            if (instance != null)
            {
                instance.name = modalPrefab != null ? modalPrefab.name : "Modal";
                modalGroup = instance.GetComponent<CanvasGroup>();
                if (modalMessageText == null)
                {
                    var msg = instance.transform.Find("Panel/Message");
                    if (msg != null) modalMessageText = msg.GetComponent<TextMeshProUGUI>();
                }
                if (modalConfirmButton == null)
                {
                    var btn = instance.transform.Find("Panel/ConfirmButton");
                    if (btn != null) modalConfirmButton = btn.GetComponent<Button>();
                }
            }
            else
            {
                modalGroup = CreateModal();
            }

            return modalGroup;
        }

        private CanvasGroup CreateModal()
        {
            var modalRoot = new GameObject("Modal", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(CanvasGroup));
            modalRoot.transform.SetParent(transform, false);

            var rootRt = modalRoot.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var rootImg = modalRoot.GetComponent<UnityEngine.UI.Image>();
            rootImg.color = new Color(0f, 0f, 0f, 0.55f);
            rootImg.raycastTarget = true;

            modalGroup = modalRoot.GetComponent<CanvasGroup>();
            modalGroup.alpha = 0f;
            modalGroup.interactable = false;
            modalGroup.blocksRaycasts = false;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            panel.transform.SetParent(modalRoot.transform, false);
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.sizeDelta = new Vector2(640f, 360f);
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;

            var panelImg = panel.GetComponent<UnityEngine.UI.Image>();
            panelImg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            panelImg.raycastTarget = true;

            var textGo = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(panel.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.1f, 0.4f);
            textRt.anchorMax = new Vector2(0.9f, 0.85f);
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            modalMessageText = textGo.GetComponent<TextMeshProUGUI>();
            modalMessageText.text = string.Empty;
            modalMessageText.fontSize = 30f;
            modalMessageText.alignment = TextAlignmentOptions.Center;
            modalMessageText.enableWordWrapping = true;

            var buttonGo = new GameObject("ConfirmButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(Button));
            buttonGo.transform.SetParent(panel.transform, false);
            var buttonRt = buttonGo.GetComponent<RectTransform>();
            buttonRt.anchorMin = new Vector2(0.3f, 0.1f);
            buttonRt.anchorMax = new Vector2(0.7f, 0.25f);
            buttonRt.offsetMin = Vector2.zero;
            buttonRt.offsetMax = Vector2.zero;

            var buttonImg = buttonGo.GetComponent<UnityEngine.UI.Image>();
            buttonImg.color = new Color(0.2f, 0.4f, 0.8f, 1f);
            buttonImg.raycastTarget = true;

            modalConfirmButton = buttonGo.GetComponent<Button>();

            var btnTextGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            btnTextGo.transform.SetParent(buttonGo.transform, false);
            var btnTextRt = btnTextGo.GetComponent<RectTransform>();
            btnTextRt.anchorMin = Vector2.zero;
            btnTextRt.anchorMax = Vector2.one;
            btnTextRt.offsetMin = Vector2.zero;
            btnTextRt.offsetMax = Vector2.zero;

            var btnLabel = btnTextGo.GetComponent<TextMeshProUGUI>();
            btnLabel.text = "확인";
            btnLabel.fontSize = 28f;
            btnLabel.alignment = TextAlignmentOptions.Center;
            btnLabel.enableWordWrapping = false;

            modalGroup.transform.SetAsLastSibling();
            return modalGroup;
        }
    }
}
