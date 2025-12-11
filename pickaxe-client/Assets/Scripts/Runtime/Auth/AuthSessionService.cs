using System.Threading.Tasks;

namespace InfinitePickaxe.Client.Auth
{
    public sealed class AuthSessionService
    {
        private readonly BackendAuthClient backendClient;
        private readonly ITokenStorage tokenStorage;
        private readonly string deviceId;
        private AuthTokens tokens;

        public AuthSessionService(BackendAuthClient backendClient, ITokenStorage tokenStorage, string deviceId = null)
        {
            this.backendClient = backendClient;
            this.tokenStorage = tokenStorage;
            this.deviceId = deviceId;
        }

        public bool HasRefreshToken => tokenStorage.HasRefreshToken();
        public bool IsAuthenticated => tokens.HasAccessToken;
        public AuthTokens Tokens => tokens;

        public void Clear()
        {
            tokens = new AuthTokens(null, null, null, null);
            tokenStorage.Clear();
        }

        public async Task<AuthResult> AuthenticateWithRefreshAsync()
        {
            if (!tokenStorage.HasRefreshToken())
            {
                return AuthResult.Fail("No refresh token");
            }

            var refresh = tokenStorage.GetRefreshToken();
            var result = await backendClient.VerifyAsync(refresh, deviceId);
            if (result.Success)
            {
                tokens = new AuthTokens(result.IdToken, result.RefreshToken, result.UserId, result.DisplayName);
                tokenStorage.SaveTokens(tokens);
            }
            return result;
        }

        public async Task<AuthResult> AuthenticateWithGoogleAsync(string googleIdToken, string deviceId)
        {
            var result = await backendClient.LoginWithGoogleAsync(googleIdToken, deviceId);
            if (result.Success)
            {
                tokens = new AuthTokens(result.IdToken, result.RefreshToken, result.UserId, result.DisplayName);
                tokenStorage.SaveTokens(tokens);
            }
            return result;
        }
    }
}
