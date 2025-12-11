using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Google;
using UnityEngine;

namespace InfinitePickaxe.Client.Auth
{
    public sealed class GoogleFirebaseAuthService : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private string webClientId = "";
        [SerializeField] private bool forceTokenRefresh;
        [SerializeField] private bool useDummyInEditor = true;
        [SerializeField] private string dummyUserId = "dev-user";
        [SerializeField] private string dummyDisplayName = "Dev User";
        [SerializeField] private string dummyGoogleIdToken = "dev-google-idtoken";
        [SerializeField] private string dummyIdToken = "dev-id-token";

        private FirebaseAuth auth;
        private bool initialized;

        public async Task<AuthResult> SignInAsync(bool silent = false)
        {
#if !UNITY_ANDROID || UNITY_EDITOR
            if (useDummyInEditor)
            {
                // Return a dummy GoogleIdToken so the backend path can still be exercised in dev.
                return AuthResult.Ok(dummyUserId, dummyDisplayName, dummyIdToken, null, $"{dummyGoogleIdToken}");
            }
            return AuthResult.Fail("Google/Firebase 로그인은 Android 디바이스에서만 지원됩니다.");
#else
            var init = await EnsureInitializedAsync();
            if (!init.Success)
            {
                return init;
            }

            ConfigureGoogleSignIn();

            try
            {
                if (!silent)
                {
                    GoogleSignIn.DefaultInstance.SignOut();
                }

                var googleUser = silent
                    ? await GoogleSignIn.DefaultInstance.SignInSilently()
                    : await GoogleSignIn.DefaultInstance.SignIn();

                var credential = GoogleAuthProvider.GetCredential(googleUser.IdToken, null);
                var firebaseUser = await auth.SignInWithCredentialAsync(credential);
                var idToken = await firebaseUser.TokenAsync(forceTokenRefresh);

                // FirebaseUser.RefreshToken is not exposed in Unity SDK. Refresh token for your auth server
                // should be obtained by exchanging idToken/googleUser.IdToken with your backend.
                return AuthResult.Ok(firebaseUser.UserId, firebaseUser.DisplayName, idToken, null, googleUser.IdToken);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Google/Firebase sign-in failed: {ex}");
                return AuthResult.Fail(ex.Message);
            }
#endif
        }

        private async Task<AuthResult> EnsureInitializedAsync()
        {
            if (initialized && auth != null)
            {
                return AuthResult.Ok(auth.CurrentUser?.UserId, auth.CurrentUser?.DisplayName, null, null, null);
            }

            var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (dependencyStatus != DependencyStatus.Available)
            {
                return AuthResult.Fail($"Firebase dependencies unavailable: {dependencyStatus}");
            }

            auth = FirebaseAuth.DefaultInstance;
            initialized = true;
            return AuthResult.Ok(null, null, null, null, null);
        }

        private void ConfigureGoogleSignIn()
        {
            GoogleSignIn.Configuration = new GoogleSignInConfiguration
            {
                WebClientId = webClientId,
                RequestEmail = true,
                RequestIdToken = true,
                RequestProfile = true,
                UseGameSignIn = false,
                ForceTokenRefresh = forceTokenRefresh
            };
        }
    }
}
