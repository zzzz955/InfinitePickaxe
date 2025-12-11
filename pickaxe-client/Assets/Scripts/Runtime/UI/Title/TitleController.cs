using System.Threading.Tasks;
using InfinitePickaxe.Client.Auth;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InfinitePickaxe.Client.UI.Title
{
    public sealed class TitleController : MonoBehaviour
    {
        [Header("View")]
        [SerializeField] private TitleView view;
        [SerializeField] private GoogleFirebaseAuthService authService;

        [Header("Scenes")]
        [SerializeField] private string gameSceneName = "Game";

        [Header("Auth")]
        [SerializeField] private bool attemptSilentSignIn = true;

        private bool autoAuthAttempted;

        private void Awake()
        {
            if (view == null)
            {
                view = GetComponentInChildren<TitleView>();
            }
        }

        private void OnEnable()
        {
            if (view == null)
            {
                Debug.LogError("TitleController: TitleView is not assigned.");
                return;
            }

            if (authService == null)
            {
                authService = GetComponent<GoogleFirebaseAuthService>();
                if (authService == null)
                {
                    Debug.LogError("TitleController: GoogleFirebaseAuthService is not assigned.");
                    return;
                }
            }

            // Ensure buttons are wired.
            view.SetButtonHandlers(OnGoogleSignInClicked);

            view.SetState(TitleState.Idle, "로그인 상태: 미인증");

            if (attemptSilentSignIn && !autoAuthAttempted)
            {
                autoAuthAttempted = true;
                _ = AutoAuthenticateAsync();
            }
        }

        private async Task AutoAuthenticateAsync()
        {
            view.SetState(TitleState.Loading, "저장된 토큰으로 자동 로그인 시도 중...");
            view.SetLoadingMessage("재인증 중...");

            var result = await authService.SignInAsync(silent: true);

            if (result.Success)
            {
                view.SetState(TitleState.Authenticated, "로그인 상태: 인증 완료");
                LoadGameScene();
            }
            else
            {
                view.SetError($"자동 로그인 실패: {result.Error}");
                view.SetState(TitleState.Idle, "로그인 상태: 미인증");
            }
        }

        public async void OnGoogleSignInClicked()
        {
            view.SetState(TitleState.Loading, "Google 로그인 진행 중...");
            view.SetLoadingMessage("Google 계정 선택...");

            var result = await authService.SignInAsync(silent: false);

            if (result.Success)
            {
                view.SetState(TitleState.Authenticated, "로그인 상태: 인증 완료");
                LoadGameScene();
            }
            else
            {
                view.SetError($"Google 로그인 실패: {result.Error}");
                view.SetState(TitleState.Idle, "로그인 상태: 미인증");
            }
        }

        private void LoadGameScene()
        {
            if (string.IsNullOrWhiteSpace(gameSceneName))
            {
                Debug.LogError("TitleController: gameSceneName is not set.");
                view.SetError("게임 씬 이름이 설정되지 않았습니다.");
                view.SetState(TitleState.Idle, "로그인 상태: 미인증");
                return;
            }

            SceneManager.LoadScene(gameSceneName);
        }
    }
}
