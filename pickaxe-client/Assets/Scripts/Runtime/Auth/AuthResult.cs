namespace InfinitePickaxe.Client.Auth
{
    public readonly struct AuthResult
    {
        public bool Success { get; }
        public string Error { get; }
        public string UserId { get; }
        public string DisplayName { get; }
        public string IdToken { get; }
        public string RefreshToken { get; }
        public string GoogleIdToken { get; }

        public AuthResult(bool success, string error, string userId, string displayName, string idToken, string refreshToken, string googleIdToken)
        {
            Success = success;
            Error = error;
            UserId = userId;
            DisplayName = displayName;
            IdToken = idToken;
            RefreshToken = refreshToken;
            GoogleIdToken = googleIdToken;
        }

        public static AuthResult Fail(string error)
        {
            return new AuthResult(false, error, null, null, null, null, null);
        }

        public static AuthResult Ok(string userId, string displayName, string idToken, string refreshToken, string googleIdToken)
        {
            return new AuthResult(true, null, userId, displayName, idToken, refreshToken, googleIdToken);
        }
    }
}
