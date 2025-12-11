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
        private System.Action onGoogleClicked;

        private TitleState state = TitleState.Idle;

        private void Awake()
        {
            ApplyState();
        }

        public void SetButtonHandlers(System.Action onGoogle)
        {
            onGoogleClicked = onGoogle;

            if (googleButton != null)
            {
                googleButton.onClick.RemoveAllListeners();
                googleButton.onClick.AddListener(() => onGoogleClicked?.Invoke());
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

            var buttonsInteractable = !isLoading && !isAuthenticated;
            if (googleButton != null)
            {
                googleButton.interactable = buttonsInteractable;
            }
        }
    }
}
