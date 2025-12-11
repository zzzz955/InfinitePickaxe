namespace InfinitePickaxe.Client.Auth
{
    public readonly struct AuthTokens
    {
        public string AccessToken { get; }
        public string RefreshToken { get; }
        public string UserId { get; }
        public string DisplayName { get; }

        public bool HasAccessToken => !string.IsNullOrWhiteSpace(AccessToken);
        public bool HasRefreshToken => !string.IsNullOrWhiteSpace(RefreshToken);

        public AuthTokens(string accessToken, string refreshToken, string userId, string displayName)
        {
            AccessToken = accessToken;
            RefreshToken = refreshToken;
            UserId = userId;
            DisplayName = displayName;
        }
    }
}
