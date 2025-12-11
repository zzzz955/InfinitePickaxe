using System.Threading.Tasks;
using InfinitePickaxe.Client.Auth;
using InfinitePickaxe.Client.Config;
using InfinitePickaxe.Client.Core;
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
        [SerializeField] private int backendTimeoutSeconds = 10;

        private bool autoAuthAttempted;
        private AuthSessionService sessionService;
        private string deviceId;

        private void Awake()
        {
            if (view == null)
            {
                view = GetComponentInChildren<TitleView>();
            }
            deviceId = SystemInfo.deviceUniqueIdentifier;
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

            sessionService = CreateSessionService();
            view.SetButtonHandlers(OnGoogleSignInClicked, OnStartClicked);
            view.SetState(TitleState.Idle, "로그인 상태: 미인증");

            if (attemptSilentSignIn && !autoAuthAttempted && sessionService.HasRefreshToken)
            {
                autoAuthAttempted = true;
                _ = AutoAuthenticateAsync();
            }
        }

        private AuthSessionService CreateSessionService()
        {
            var config = ClientRuntime.IsInitialized ? ClientRuntime.Config : ClientConfigLoader.Load();
            var env = config.GetActiveEnvironment();
            var scheme = env.useTls ? "https" : "http";
            var baseUri = $"{scheme}://{env.host}:{env.port}";
            var backend = new BackendAuthClient(baseUri, backendTimeoutSeconds);
            var storage = new PlayerPrefsTokenStorage();
            return new AuthSessionService(backend, storage);
        }

        private async Task AutoAuthenticateAsync()
        {
            view.SetState(TitleState.Loading, "저장된 토큰으로 자동 로그인 시도 중...");
            view.SetLoadingMessage("재인증 중...");
            view.ShowOverlay(true, "토큰으로 인증 중...");

            var result = await sessionService.AuthenticateWithRefreshAsync();
            if (result.Success)
            {
                view.SetState(TitleState.Authenticated, "로그인 상태: 인증 완료");
                view.ShowOverlay(false);
            }
            else
            {
                view.SetError($"자동 로그인 실패: {result.Error}");
                view.SetState(TitleState.Idle, "로그인 상태: 미인증");
                view.ShowOverlay(false);
            }
        }

        public async void OnGoogleSignInClicked()
        {
            view.SetState(TitleState.Loading, "Google 로그인 진행 중...");
            view.SetLoadingMessage("Google 계정 선택...");
            view.ShowOverlay(true, "로그인 진행 중...");

            var result = await authService.SignInAsync(silent: false);
            if (!result.Success)
            {
                view.SetError($"Google 로그인 실패: {result.Error}");
                view.SetState(TitleState.Idle, "로그인 상태: 미인증");
                view.ShowOverlay(false);
                return;
            }

            var backendResult = await sessionService.AuthenticateWithGoogleAsync(result.GoogleIdToken, deviceId);
            if (!backendResult.Success)
            {
                view.SetError($"백엔드 인증 실패: {backendResult.Error}");
                view.SetState(TitleState.Idle, "로그인 상태: 미인증");
                view.ShowOverlay(false);
                return;
            }

            view.SetState(TitleState.Authenticated, "로그인 상태: 인증 완료");
            view.ShowOverlay(false);
        }

        public void OnStartClicked()
        {
            view.ShowOverlay(true, "게임 데이터 불러오는 중...");
            LoadGameScene();
        }

        private void LoadGameScene()
        {
            if (string.IsNullOrWhiteSpace(gameSceneName))
            {
                Debug.LogError("TitleController: gameSceneName is not set.");
                view.SetError("게임 씬 이름이 설정되지 않았습니다.");
                view.SetState(TitleState.Idle, "로그인 상태: 미인증");
                view.ShowOverlay(false);
                return;
            }

            SceneManager.LoadScene(gameSceneName);
        }
    }
}
