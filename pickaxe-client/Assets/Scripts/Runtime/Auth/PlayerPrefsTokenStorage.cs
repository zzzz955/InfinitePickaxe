using UnityEngine;

namespace InfinitePickaxe.Client.Auth
{
    public sealed class PlayerPrefsTokenStorage : ITokenStorage
    {
        private readonly string refreshKey;
        private readonly string accessKey;

        public PlayerPrefsTokenStorage(string refreshKey = "auth_jwt", string accessKey = "auth_access_token")
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
