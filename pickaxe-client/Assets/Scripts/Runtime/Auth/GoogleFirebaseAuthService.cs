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

        private FirebaseAuth auth;
        private bool initialized;

        public async Task<AuthResult> SignInAsync(bool silent = false)
        {
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
                return AuthResult.Ok(firebaseUser.UserId, firebaseUser.DisplayName, idToken, googleUser.IdToken);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Google/Firebase sign-in failed: {ex}");
                return AuthResult.Fail(ex.Message);
            }
        }

        private async Task<AuthResult> EnsureInitializedAsync()
        {
            if (initialized && auth != null)
            {
                return AuthResult.Ok(auth.CurrentUser?.UserId, auth.CurrentUser?.DisplayName, null, null);
            }

            var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (dependencyStatus != DependencyStatus.Available)
            {
                return AuthResult.Fail($"Firebase dependencies unavailable: {dependencyStatus}");
            }

            auth = FirebaseAuth.DefaultInstance;
            initialized = true;
            return AuthResult.Ok(null, null, null, null);
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
