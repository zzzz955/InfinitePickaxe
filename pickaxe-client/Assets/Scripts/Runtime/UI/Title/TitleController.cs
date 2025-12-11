using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InfinitePickaxe.Client.UI.Title
{
    public sealed class TitleController : MonoBehaviour
    {
        [Header("View")]
        [SerializeField] private TitleView view;

        [Header("Scenes")]
        [SerializeField] private string gameSceneName = "Game";

        [Header("Auth")]
        [SerializeField] private string refreshTokenPlayerPrefsKey = "refresh_token";

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

            // Ensure buttons are wired.
            view.SetButtonHandlers(OnGoogleSignInClicked);

            view.SetState(TitleState.Idle, "로그인 상태: 미인증");

            if (!autoAuthAttempted && HasSavedRefreshToken())
            {
                autoAuthAttempted = true;
                _ = AutoAuthenticateAsync();
            }
        }

        private bool HasSavedRefreshToken()
        {
            return PlayerPrefs.HasKey(refreshTokenPlayerPrefsKey) &&
                   !string.IsNullOrWhiteSpace(PlayerPrefs.GetString(refreshTokenPlayerPrefsKey));
        }

        private async Task AutoAuthenticateAsync()
        {
            view.SetState(TitleState.Loading, "저장된 토큰으로 자동 로그인 시도 중...");
            view.SetLoadingMessage("재인증 중...");

            var success = await SimulateAuthAsync();

            if (success)
            {
                view.SetState(TitleState.Authenticated, "로그인 상태: 인증 완료");
                LoadGameScene();
            }
            else
            {
                view.SetError("자동 로그인 실패: 다시 시도해주세요.");
                view.SetState(TitleState.Idle, "로그인 상태: 미인증");
            }
        }

        public async void OnGoogleSignInClicked()
        {
            view.SetState(TitleState.Loading, "Google 로그인 진행 중...");
            view.SetLoadingMessage("Google 계정 선택...");

            var success = await SimulateAuthAsync();

            if (success)
            {
                // Save placeholder refresh token for future auto-auth.
                PlayerPrefs.SetString(refreshTokenPlayerPrefsKey, "mock_refresh_token");
                PlayerPrefs.Save();

                view.SetState(TitleState.Authenticated, "로그인 상태: 인증 완료");
                LoadGameScene();
            }
            else
            {
                view.SetError("Google 로그인 실패: 다시 시도해주세요.");
                view.SetState(TitleState.Idle, "로그인 상태: 미인증");
            }
        }

        private async Task<bool> SimulateAuthAsync()
        {
            // Placeholder for real auth flow (GPG/Firebase + handshake).
            await Task.Delay(1000);
            return true;
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
