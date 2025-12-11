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
        [SerializeField] private Button logoutButton;
        [SerializeField] private CanvasGroup overlayGroup;
        [SerializeField] private GameObject modalPrefab;
        [SerializeField] private string modalResourcePath = "UI/Modal";
        [SerializeField] private CanvasGroup modalGroup;
        [SerializeField] private TextMeshProUGUI modalMessageText;
        [SerializeField] private TextMeshProUGUI modalStatusText;
        [SerializeField] private Button modalConfirmButton;
        [SerializeField] private Button modalCancelButton;
        [SerializeField] private TMP_InputField modalInputField;
        [SerializeField] private bool preserveModalLayout = true;

        private System.Action onGoogleClicked;
        private System.Action onStartClicked;
        private System.Action onLogoutClicked;
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
            AlignStartButtonWithGoogle();
            if (overlayGroup == null)
            {
                overlayGroup = CreateOverlay();
            }

            EnsureModalInstance();

            ApplyState();
        }

        public void SetButtonHandlers(System.Action onGoogle, System.Action onStart, System.Action onLogout)
        {
            onGoogleClicked = onGoogle;
            onStartClicked = onStart;
            onLogoutClicked = onLogout;

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

            if (logoutButton != null)
            {
                logoutButton.onClick.RemoveAllListeners();
                logoutButton.onClick.AddListener(() => onLogoutClicked?.Invoke());
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

            SetModalMessage(string.IsNullOrEmpty(message)
                ? "알 수 없는 오류가 발생했습니다."
                : message);
            SetModalStatus(string.Empty, false);

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

            if (modalInputField != null)
            {
                modalInputField.gameObject.SetActive(false);
            }

            modalGroup.gameObject.SetActive(true);
            modalGroup.alpha = 1f;
            modalGroup.interactable = true;
            modalGroup.blocksRaycasts = true;
            modalGroup.transform.SetAsLastSibling();
        }

        public void ShowInputModal(string message, string placeholder, string buttonText, System.Action<string> onSubmit, System.Action onCancel = null)
        {
            EnsureModalInstance();

            SetModalMessage(string.IsNullOrEmpty(message)
                ? "정보를 입력해주세요."
                : message);
            SetModalStatus(string.Empty, false);

            if (modalInputField != null)
            {
                modalInputField.gameObject.SetActive(true);
                modalInputField.text = string.Empty;
                var ph = modalInputField.placeholder as TextMeshProUGUI;
                if (ph != null)
                {
                    ph.text = string.IsNullOrEmpty(placeholder) ? "입력" : placeholder;
                }
            }
            else
            {
                Debug.LogError("TitleView: modalInputField is not assigned. Please wire the InputField from the prefab.");
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
                    var value = modalInputField != null ? modalInputField.text?.Trim() : string.Empty;
                    if (string.IsNullOrEmpty(value))
                    {
                        SetModalStatus("올바른 닉네임을 입력해주세요.");
                        return;
                    }
                    onSubmit?.Invoke(value);
                });
            }
            else
            {
                Debug.LogError("TitleView: modalConfirmButton is not assigned. Please wire the ConfirmButton from the prefab.");
            }

            if (modalCancelButton != null)
            {
                modalCancelButton.onClick.RemoveAllListeners();
                modalCancelButton.onClick.AddListener(() =>
                {
                    HideModal();
                    onCancel?.Invoke();
                });
            }
            else if (onCancel != null)
            {
                Debug.LogWarning("TitleView: modalCancelButton is not assigned; cancel action will not be invoked.");
            }

            modalGroup.gameObject.SetActive(true);
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
            modalGroup.gameObject.SetActive(false);
        }

        public void SetModalMessage(string message)
        {
            EnsureModalInstance();
            if (modalMessageText != null)
            {
                modalMessageText.text = message ?? string.Empty;
            }
        }

        public void SetModalStatus(string message, bool show = true, Color? color = null)
        {
            EnsureModalInstance();
            if (modalStatusText == null)
            {
                return;
            }

            var hasMessage = !string.IsNullOrEmpty(message);
            modalStatusText.gameObject.SetActive(show && hasMessage);
            if (show && hasMessage)
            {
                modalStatusText.text = message;
                modalStatusText.color = color ?? new Color(1f, 0.6f, 0.6f, 1f);
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

            if (logoutButton != null)
            {
                logoutButton.gameObject.SetActive(isAuthenticated && !isLoading);
                logoutButton.interactable = buttonsInteractable && isAuthenticated;
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
            rt.anchoredPosition = source.GetComponent<RectTransform>()?.anchoredPosition ?? rt.anchoredPosition;

            var text = clone.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = "게임 시작";
            }

            var btn = clone.GetComponent<Button>();
            return btn;
        }

        private void AlignStartButtonWithGoogle()
        {
            if (startButton == null || googleButton == null)
            {
                return;
            }

            if (googleButton.TryGetComponent(out RectTransform sourceRt) &&
                startButton.TryGetComponent(out RectTransform targetRt))
            {
                targetRt.anchorMin = sourceRt.anchorMin;
                targetRt.anchorMax = sourceRt.anchorMax;
                targetRt.sizeDelta = sourceRt.sizeDelta;
                targetRt.anchoredPosition = sourceRt.anchoredPosition;
            }
        }

        private CanvasGroup EnsureModalInstance()
        {
            if (modalGroup != null)
            {
                return modalGroup;
            }

            // Expect the modal to be placed in the scene and references assigned in the inspector.
            modalGroup = modalPrefab != null
                ? modalPrefab.GetComponent<CanvasGroup>() ?? modalPrefab.GetComponentInChildren<CanvasGroup>()
                : GetComponentInChildren<CanvasGroup>(true);

            if (modalGroup == null)
            {
                Debug.LogError("TitleView: modalGroup is not assigned. Drag the modal prefab into the scene and wire fields in the inspector.");
                return null;
            }

            var panel = modalGroup.transform.Find("Panel");
            if (modalMessageText == null)
            {
                var msg = modalGroup.transform.Find("Panel/Message");
                if (msg != null) modalMessageText = msg.GetComponent<TextMeshProUGUI>();
            }
            if (modalConfirmButton == null)
            {
                var btn = modalGroup.transform.Find("Panel/ConfirmButton");
                if (btn != null) modalConfirmButton = btn.GetComponent<Button>();
            }
            EnsureInputField(panel);
            EnsureStatusLabel(panel);

            return modalGroup;
        }

        private CanvasGroup CreateModal(Transform parent)
        {
            Debug.LogError("TitleView: No modal assigned. Please place the modal prefab in the scene and assign references.");
            return null;
        }

        private Transform GetCanvasTransform()
        {
            var canvas = GetComponentInParent<Canvas>();
            return canvas != null ? canvas.transform : transform;
        }

        private TextMeshProUGUI CreateInputText(Transform parent)
        {
            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(parent, false);
            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(10f, 10f);
            rt.offsetMax = new Vector2(-10f, -10f);

            var txt = textGo.GetComponent<TextMeshProUGUI>();
            txt.text = string.Empty;
            txt.fontSize = 28f;
            txt.alignment = TextAlignmentOptions.MidlineLeft;
            txt.enableWordWrapping = false;
            return txt;
        }

        private TextMeshProUGUI CreatePlaceholder(Transform parent)
        {
            var phGo = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
            phGo.transform.SetParent(parent, false);
            var rt = phGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(10f, 10f);
            rt.offsetMax = new Vector2(-10f, -10f);

            var txt = phGo.GetComponent<TextMeshProUGUI>();
            txt.text = "입력";
            txt.fontSize = 24f;
            txt.alignment = TextAlignmentOptions.MidlineLeft;
            txt.color = new Color(1f, 1f, 1f, 0.5f);
            return txt;
        }

        private void EnsureInputField(Transform panel)
        {
            if (modalInputField != null)
            {
                StyleInputField(modalInputField);
                return;
            }

            if (panel == null) return;

            var existing = panel.Find("InputField");
            if (existing != null)
            {
                modalInputField = existing.GetComponent<TMP_InputField>();
                if (modalInputField == null)
                {
                    Debug.LogError("TitleView: InputField object exists but TMP_InputField is missing. Please add TMP_InputField component.");
                    return;
                }
                StyleInputField(modalInputField);
                return;
            }

            Debug.LogError("TitleView: InputField not found under modal Panel. Please place the prefab in the scene and assign the fields.");
        }

        private void StyleInputField(TMP_InputField field)
        {
            if (preserveModalLayout)
            {
                return;
            }

            var rt = field.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.1f, 0.32f);
                rt.anchorMax = new Vector2(0.9f, 0.48f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            var bg = field.GetComponent<UnityEngine.UI.Image>();
            if (bg != null)
            {
                bg.color = new Color(0.18f, 0.28f, 0.45f, 1f);
            }

            var font = modalMessageText != null ? modalMessageText.font : null;
            if (field.textComponent != null)
            {
                if (font != null) field.textComponent.font = font;
                field.textComponent.color = Color.white;
                field.textComponent.fontSize = 28f;
            }
            if (field.placeholder is TextMeshProUGUI ph)
            {
                if (font != null) ph.font = font;
                ph.color = new Color(1f, 1f, 1f, 0.7f);
                ph.fontSize = 24f;
            }
        }

        private void EnsureStatusLabel(Transform panel)
        {
            if (modalStatusText != null)
            {
                StyleStatusLabel(modalStatusText);
                return;
            }

            if (panel == null) return;

            var existing = panel.Find("Status");
            if (existing != null)
            {
                modalStatusText = existing.GetComponent<TextMeshProUGUI>();
                if (modalStatusText != null)
                {
                    StyleStatusLabel(modalStatusText);
                }
                return;
            }

            Debug.LogWarning("TitleView: Status label not found under modal Panel. Add a Status TextMeshProUGUI to show validation messages.");
        }

        private void StyleStatusLabel(TextMeshProUGUI label)
        {
            if (label == null)
            {
                return;
            }

            if (preserveModalLayout)
            {
                label.gameObject.SetActive(false);
                return;
            }

            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableWordWrapping = true;
            label.fontSize = 24f;
            label.color = new Color(1f, 0.6f, 0.6f, 1f);
            if (modalMessageText != null)
            {
                label.font = modalMessageText.font;
            }
            if (label.rectTransform != null)
            {
                label.rectTransform.anchorMin = new Vector2(0.1f, 0.18f);
                label.rectTransform.anchorMax = new Vector2(0.9f, 0.26f);
                label.rectTransform.offsetMin = Vector2.zero;
                label.rectTransform.offsetMax = Vector2.zero;
            }
            label.gameObject.SetActive(false);
        }

        private void EnsureCanvasOnModal(GameObject instance)
        {
            if (instance == null) return;

            var canvas = instance.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = 1000;
            }
        }

        private void ApplyModalFonts()
        {
            if (preserveModalLayout)
            {
                return;
            }

            var baseFont = statusText != null ? statusText.font : modalMessageText != null ? modalMessageText.font : null;
            if (baseFont == null)
            {
                return;
            }

            if (modalMessageText != null)
            {
                modalMessageText.font = baseFont;
            }

            if (modalStatusText != null)
            {
                modalStatusText.font = baseFont;
            }

            if (modalInputField != null)
            {
                if (modalInputField.textComponent != null)
                {
                    modalInputField.textComponent.font = baseFont;
                }
                if (modalInputField.placeholder is TextMeshProUGUI ph)
                {
                    ph.font = baseFont;
                }
            }

            if (modalConfirmButton != null)
            {
                var label = modalConfirmButton.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.font = baseFont;
                }
            }
        }

        private void StyleModalLayout()
        {
            if (preserveModalLayout)
            {
                return;
            }

            if (modalMessageText != null && modalMessageText.rectTransform != null)
            {
                var rt = modalMessageText.rectTransform;
                rt.anchorMin = new Vector2(0.1f, 0.55f);
                rt.anchorMax = new Vector2(0.9f, 0.88f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            if (modalConfirmButton != null && modalConfirmButton.transform is RectTransform btnRt)
            {
                btnRt.anchorMin = new Vector2(0.3f, 0.1f);
                btnRt.anchorMax = new Vector2(0.7f, 0.17f);
                btnRt.offsetMin = Vector2.zero;
                btnRt.offsetMax = Vector2.zero;
            }
        }
    }
}
