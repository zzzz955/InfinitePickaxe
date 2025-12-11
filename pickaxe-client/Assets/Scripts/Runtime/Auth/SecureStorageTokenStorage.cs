using UnityEngine;

namespace InfinitePickaxe.Client.Auth
{
    /// <summary>
    /// Secure token storage for devices; falls back to PlayerPrefs in the Editor.
    /// </summary>
    public sealed class SecureStorageTokenStorage : ITokenStorage
    {
        private const string RefreshKey = "secure_refresh_token";
        private const string AccessKey = "secure_access_token";
        private const string RefreshAlias = "infinitepickaxe_refresh";
        private const string AccessAlias = "infinitepickaxe_access";

        private readonly PlayerPrefsTokenStorage fallback = new PlayerPrefsTokenStorage();

        public bool HasRefreshToken()
        {
#if UNITY_EDITOR
            return fallback.HasRefreshToken();
#elif UNITY_ANDROID
            var cipher = PlayerPrefs.GetString(RefreshKey, string.Empty);
            if (string.IsNullOrEmpty(cipher))
            {
                return false;
            }
            var plain = AndroidSecureStorage.Decrypt(RefreshAlias, cipher);
            return !string.IsNullOrEmpty(plain);
#else
            return fallback.HasRefreshToken();
#endif // UNITY_EDITOR
        }

        public string GetRefreshToken()
        {
#if UNITY_EDITOR
            return fallback.GetRefreshToken();
#elif UNITY_ANDROID
            return ReadAndMaybeClean(RefreshKey, RefreshAlias);
#else
            return fallback.GetRefreshToken();
#endif // UNITY_EDITOR
        }

        public void SaveTokens(AuthTokens tokens)
        {
#if UNITY_EDITOR
            fallback.SaveTokens(tokens);
#elif UNITY_ANDROID
            if (!string.IsNullOrEmpty(tokens.RefreshToken))
            {
                var cipher = AndroidSecureStorage.Encrypt(RefreshAlias, tokens.RefreshToken);
                if (!string.IsNullOrEmpty(cipher))
                {
                    PlayerPrefs.SetString(RefreshKey, cipher);
                }
            }

            if (!string.IsNullOrEmpty(tokens.AccessToken))
            {
                var cipher = AndroidSecureStorage.Encrypt(AccessAlias, tokens.AccessToken);
                if (!string.IsNullOrEmpty(cipher))
                {
                    PlayerPrefs.SetString(AccessKey, cipher);
                }
            }
            PlayerPrefs.Save();
#else
            fallback.SaveTokens(tokens);
#endif // UNITY_EDITOR
        }

        public void Clear()
        {
#if UNITY_EDITOR
            fallback.Clear();
#elif UNITY_ANDROID
            PlayerPrefs.DeleteKey(RefreshKey);
            PlayerPrefs.DeleteKey(AccessKey);
#else
            fallback.Clear();
#endif // UNITY_EDITOR
        }

        private static string ReadAndMaybeClean(string key, string alias)
        {
            var cipher = PlayerPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(cipher))
            {
                return string.Empty;
            }

            var plain = AndroidSecureStorage.Decrypt(alias, cipher);
            if (string.IsNullOrEmpty(plain))
            {
                PlayerPrefs.DeleteKey(key);
            }
            return plain;
        }
    }
}
