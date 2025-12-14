using System;
using System.Threading.Tasks;
using InfinitePickaxe.Client.Auth;
using InfinitePickaxe.Client.Config;
using InfinitePickaxe.Client.Core;
using InfinitePickaxe.Client.UI.Title;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InfinitePickaxe.Client.Core
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

        private static string pendingReconnectMessage;

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

            sessionService = ResolveSessionService();
            view.SetButtonHandlers(OnGoogleSignInClicked, OnStartClicked, OnLogoutClicked);
            view.SetState(TitleState.Idle, IdleStatus);

            if (!string.IsNullOrEmpty(pendingReconnectMessage))
            {
                view.ShowModal(pendingReconnectMessage, "확인", () =>
                {
                    view.HideModal();
                    pendingReconnectMessage = null;
                });
            }

            if (attemptSilentSignIn && !autoAuthAttempted && sessionService.HasRefreshToken)
            {
                autoAuthAttempted = true;
                _ = AutoAuthenticateAsync();
            }
        }

        public static void SetReconnectNotice(string message)
        {
            pendingReconnectMessage = message;
        }

        private AuthSessionService ResolveSessionService()
        {
            if (ClientRuntime.TryResolve(out AuthSessionService existing))
            {
                return existing;
            }

            var created = CreateSessionService();
            if (ClientRuntime.IsInitialized)
            {
                try
                {
                    ClientRuntime.Services.RegisterSingleton(created);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"TitleController: AuthSessionService 등록 실패, 로컬 인스턴스로 계속합니다. {ex.Message}");
                }
            }

            return created;
        }

        private AuthSessionService CreateSessionService()
        {
            var config = ClientRuntime.IsInitialized ? ClientRuntime.Config : ClientConfigLoader.Load();
            var env = config.GetActiveEnvironment();
            var scheme = env.useTls ? "https" : "http";
            var authPort = env.authPort > 0 ? env.authPort : env.port;
            var baseUri = $"{scheme}://{env.host}:{authPort}";
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
                OnAuthenticated(result.Nickname);
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

            var provider = string.IsNullOrEmpty(result.Provider) ? "google" : result.Provider;
            var backendResult = await sessionService.AuthenticateWithProviderAsync(provider, result.GoogleIdToken, deviceId, result.Email);
            if (!backendResult.Success)
            {
                HandleInvalidToken($"백엔드 인증 실패: {backendResult.Error}");
                return;
            }

            OnAuthenticated(backendResult.Nickname);
        }

        public void OnStartClicked()
        {
            _ = StartGameFlowAsync();
        }

        public void OnLogoutClicked()
        {
            sessionService.Clear();
            view.ShowOverlay(true, "로그아웃 처리 중...");
            view.SetState(TitleState.Idle, IdleStatus);
            view.ShowOverlay(false);
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

        private void OnAuthenticated(string nickname)
        {
            if (!string.IsNullOrEmpty(nickname))
            {
                view.ShowOverlay(false);
                view.SetState(TitleState.Authenticated, $"환영합니다 {nickname}님!");
            }
            else
            {
                view.SetState(TitleState.Loading, "닉네임을 설정해주세요.");
                view.ShowOverlay(true, "닉네임 설정 필요");
                PromptNickname();
            }
        }

        private void PromptNickname()
        {
            view.ShowOverlay(true, "닉네임 설정 중...");
            view.ShowInputModal(
                "닉네임을 설정해주세요.",
                "닉네임",
                "확인",
                async nickname =>
                {
                    var result = await sessionService.UpdateNicknameAsync(nickname);
                    view.ShowOverlay(false);
                    if (result.Success && sessionService.Tokens.HasNickname)
                    {
                        view.HideModal();
                        view.SetState(TitleState.Authenticated, $"환영합니다 {sessionService.Tokens.Nickname}님!");
                    }
                    else
                    {
                        view.SetModalStatus($"닉네임 설정 실패: {result.Error}");
                    }
                },
                () =>
                {
                    view.HideModal();
                    OnLogoutClicked();
                });
        }

        private void HandleInvalidToken(string message)
        {
            sessionService?.Clear();
            var display = string.IsNullOrEmpty(message)
                ? "세션이 만료되었습니다. 다시 로그인해주세요."
                : message;
            view.SetError(display);
            view.SetState(TitleState.Idle, "로그인 상태: 재로그인 필요");
            view.ShowOverlay(false);
            view.ShowModal(display, "다시 로그인");
        }

        private async Task StartGameFlowAsync()
        {
            if (!sessionService.IsAuthenticated)
            {
                HandleInvalidToken("로그인이 필요합니다.");
                return;
            }

            if (!sessionService.Tokens.HasNickname)
            {
                PromptNickname();
                return;
            }

            view.ShowOverlay(true, "게임 서버 연결 준비 중...");

            var refreshResult = await sessionService.RefreshAccessTokenIfNeededAsync(120);
            if (!refreshResult.Success)
            {
                view.ShowOverlay(false);
                view.ShowModal($"토큰 갱신 실패: {refreshResult.Error}", "다시 로그인", () =>
                {
                    HandleInvalidToken(refreshResult.Error);
                });
                return;
            }

            view.ShowOverlay(true, "게임 데이터를 불러오는 중...");
            LoadGameScene();
        }
    }
}
