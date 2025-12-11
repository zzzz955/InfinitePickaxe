namespace InfinitePickaxe.Client.Auth
{
    public readonly struct AuthTokens
    {
        public string AccessToken { get; }
        public string RefreshToken { get; }
        public string UserId { get; }
        public string Nickname { get; }
        public string Email { get; }

        public bool HasAccessToken => !string.IsNullOrWhiteSpace(AccessToken);
        public bool HasRefreshToken => !string.IsNullOrWhiteSpace(RefreshToken);
        public bool HasNickname => !string.IsNullOrWhiteSpace(Nickname);

        public AuthTokens(string accessToken, string refreshToken, string userId, string nickname = null, string email = null)
        {
            AccessToken = accessToken;
            RefreshToken = refreshToken;
            UserId = userId;
            Nickname = nickname;
            Email = email;
        }
    }
}
