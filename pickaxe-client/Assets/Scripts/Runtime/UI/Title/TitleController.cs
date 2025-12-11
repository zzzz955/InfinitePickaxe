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

        private const string IdleStatus = "로그인 상태: 미인증";
        private const string AuthenticatedStatus = "로그인 상태: 인증 완료";

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
            view.SetState(TitleState.Idle, IdleStatus);

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
            ITokenStorage storage;
#if UNITY_EDITOR
            storage = new PlayerPrefsTokenStorage();
#else
            storage = new SecureStorageTokenStorage();
#endif
            return new AuthSessionService(backend, storage, deviceId);
        }

        private async Task AutoAuthenticateAsync()
        {
            view.SetState(TitleState.Loading, "저장된 토큰으로 자동 로그인 중...");
            view.SetLoadingMessage("세션 검증 중...");
            view.ShowOverlay(true, "세션 검증 중...");

            var result = await sessionService.AuthenticateWithRefreshAsync();
            if (result.Success)
            {
                view.SetState(TitleState.Authenticated, AuthenticatedStatus);
                view.ShowOverlay(false);
            }
            else
            {
                HandleInvalidToken($"자동 로그인 실패: {result.Error}");
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
                HandleInvalidToken($"Google 로그인 실패: {result.Error}");
                return;
            }

            var backendResult = await sessionService.AuthenticateWithGoogleAsync(result.GoogleIdToken, deviceId);
            if (!backendResult.Success)
            {
                HandleInvalidToken($"백엔드 인증 실패: {backendResult.Error}");
                return;
            }

            view.SetState(TitleState.Authenticated, AuthenticatedStatus);
            view.ShowOverlay(false);
        }

        public void OnStartClicked()
        {
            view.ShowOverlay(true, "게임 데이터를 불러오는 중...");
            LoadGameScene();
        }

        private void LoadGameScene()
        {
            if (string.IsNullOrWhiteSpace(gameSceneName))
            {
                Debug.LogError("TitleController: gameSceneName is not set.");
                view.SetError("게임 씬 이름이 설정되지 않았습니다.");
                view.SetState(TitleState.Idle, IdleStatus);
                view.ShowOverlay(false);
                return;
            }

            SceneManager.LoadScene(gameSceneName);
        }

        private void HandleInvalidToken(string message)
        {
            sessionService?.Clear();
            view.SetError(string.IsNullOrEmpty(message)
                ? "세션이 만료되었습니다. 다시 로그인해주세요."
                : message);
            view.SetState(TitleState.Idle, "로그인 상태: 재로그인 필요");
            view.ShowOverlay(false);
        }
    }
}
