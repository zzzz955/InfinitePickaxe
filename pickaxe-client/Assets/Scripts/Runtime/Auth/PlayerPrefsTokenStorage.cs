using UnityEngine;

namespace InfinitePickaxe.Client.Auth
{
    /// <summary>
    /// Simple token storage using PlayerPrefs. Replace with secure storage (KeyStore/Keychain) for production.
    /// </summary>
    public sealed class PlayerPrefsTokenStorage
    {
        private readonly string refreshKey;
        private readonly string accessKey;

        public PlayerPrefsTokenStorage(string refreshKey = "auth_refresh_token", string accessKey = "auth_access_token")
        {
            this.refreshKey = refreshKey;
            this.accessKey = accessKey;
        }

        public bool HasRefreshToken()
        {
            return PlayerPrefs.HasKey(refreshKey) && !string.IsNullOrWhiteSpace(PlayerPrefs.GetString(refreshKey));
        }

        public string GetRefreshToken()
        {
            return PlayerPrefs.GetString(refreshKey, string.Empty);
        }

        public void SaveTokens(AuthTokens tokens)
        {
            if (!string.IsNullOrEmpty(tokens.RefreshToken))
            {
                PlayerPrefs.SetString(refreshKey, tokens.RefreshToken);
            }
            if (!string.IsNullOrEmpty(tokens.AccessToken))
            {
                PlayerPrefs.SetString(accessKey, tokens.AccessToken);
            }
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(refreshKey);
            PlayerPrefs.DeleteKey(accessKey);
        }
    }
}
