namespace InfinitePickaxe.Client.Auth
{
    public readonly struct AuthResult
    {
        public bool Success { get; }
        public string Error { get; }
        public string UserId { get; }
        public string Nickname { get; }
        public string Email { get; }
        public string Provider { get; }
        public string IdToken { get; }
        public string RefreshToken { get; }
        public string GoogleIdToken { get; }

        public AuthResult(bool success, string error, string userId, string nickname, string email, string provider, string idToken, string refreshToken, string googleIdToken)
        {
            Success = success;
            Error = error;
            UserId = userId;
            Nickname = nickname;
            Email = email;
            Provider = provider;
            IdToken = idToken;
            RefreshToken = refreshToken;
            GoogleIdToken = googleIdToken;
        }

        public static AuthResult Fail(string error)
        {
            return new AuthResult(false, error, null, null, null, null, null, null, null);
        }

        public static AuthResult Ok(string userId, string nickname, string email, string provider, string idToken, string refreshToken, string googleIdToken)
        {
            return new AuthResult(true, null, userId, nickname, email, provider, idToken, refreshToken, googleIdToken);
        }
    }
}
