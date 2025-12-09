using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Google;
using Pickaxe.Client.Core;
using UnityEngine;

namespace Pickaxe.Client.Services
{
    /// <summary>
    /// Google Sign-In + Firebase Auth 래퍼. One Tap/네이티브 계정 선택으로 ID 토큰을 받아 서버에 전달하는 용도.
    /// </summary>
    public class GoogleAuthService : MonoBehaviour
    {
        public static GoogleAuthService Instance { get; private set; }

        private FirebaseAuth _auth;
        private bool _initialized;
        private AuthConfig _config;
        private bool IsAndroidRuntime => Application.platform == RuntimePlatform.Android;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public async Task<bool> InitializeAsync()
        {
            if (_initialized) return true;

            if (!IsAndroidRuntime)
            {
                Debug.LogWarning("GoogleAuthService: Android 런타임에서만 초기화합니다.");
                return false;
            }

            _config = AuthConfig.Load();
            if (_config == null || string.IsNullOrEmpty(_config.webClientId))
            {
                Debug.LogError("AuthConfig 로드 실패 또는 WebClientId 누락");
                return false;
            }

            var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (dependencyStatus != DependencyStatus.Available)
            {
                Debug.LogError($"Firebase 종속성 확인 실패: {dependencyStatus}");
                return false;
            }

            _auth = FirebaseAuth.DefaultInstance;
            ConfigureGoogleSignIn(_config);
            _initialized = true;
            return true;
        }

        private void ConfigureGoogleSignIn(AuthConfig cfg)
        {
            GoogleSignIn.Configuration = new GoogleSignInConfiguration
            {
                WebClientId = cfg.webClientId,
                RequestIdToken = cfg.requestIdToken,
                ForceTokenRefresh = cfg.forceTokenRefresh,
                UseGameSignIn = false,
                RequestEmail = true,
                RequestAuthCode = false
            };
        }

        public async Task<GoogleSignInUser> SignInAsync()
        {
            if (!_initialized && !await InitializeAsync())
            {
                return null;
            }
            if (!IsAndroidRuntime)
            {
                Debug.LogWarning("GoogleAuthService: Android 런타임이 아니므로 Sign-In을 건너뜁니다.");
                return null;
            }
            try
            {
                var gUser = await GoogleSignIn.DefaultInstance.SignIn();
                var credential = GoogleAuthProvider.GetCredential(gUser.IdToken, null);
                await _auth.SignInWithCredentialAsync(credential);
                Debug.Log($"Google Sign-In 성공: {gUser.UserId}, 이메일: {gUser.Email}");
                return gUser;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Google Sign-In 실패: {ex}");
                return null;
            }
        }

        public void SignOut()
        {
            if (!IsAndroidRuntime)
            {
                Debug.LogWarning("GoogleAuthService: Android 런타임이 아니므로 Sign-Out을 건너뜁니다.");
                return;
            }
            try
            {
                _auth?.SignOut();
                GoogleSignIn.DefaultInstance.SignOut();
                Debug.Log("Sign-Out 완료");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Sign-Out 실패: {ex}");
            }
        }

        public FirebaseUser CurrentUser => _auth?.CurrentUser;
    }
}
