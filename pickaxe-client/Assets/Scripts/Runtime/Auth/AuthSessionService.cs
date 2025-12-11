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
            tokens = new AuthTokens(null, null, null);
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
                tokens = new AuthTokens(result.IdToken, result.RefreshToken, result.UserId, result.Nickname, result.Email);
                tokenStorage.SaveTokens(tokens);
            }
            return result;
        }

        public async Task<AuthResult> AuthenticateWithProviderAsync(string provider, string token, string deviceId, string email)
        {
            var result = await backendClient.LoginAsync(provider, token, deviceId, email);
            if (result.Success)
            {
                tokens = new AuthTokens(result.IdToken, result.RefreshToken, result.UserId, result.Nickname, result.Email);
                tokenStorage.SaveTokens(tokens);
            }
            return result;
        }

        public async Task<AuthResult> UpdateNicknameAsync(string nickname)
        {
            if (!tokens.HasAccessToken)
            {
                return AuthResult.Fail("로그인이 필요합니다.");
            }

            var result = await backendClient.SetNicknameAsync(tokens.AccessToken, nickname);
            if (result.Success)
            {
                tokens = new AuthTokens(tokens.AccessToken, tokens.RefreshToken, tokens.UserId, result.Nickname, tokens.Email);
                tokenStorage.SaveTokens(tokens);
            }
            return result;
        }
    }
}
