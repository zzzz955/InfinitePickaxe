using System.Threading.Tasks;
using System;

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

        public async Task<AuthResult> RefreshAccessTokenIfNeededAsync(int thresholdSeconds = 120)
        {
            if (!tokens.HasRefreshToken)
            {
                return AuthResult.Fail("리프레시 토큰이 없습니다.");
            }

            // JWT exp 확인, 남은 시간이 thresholdSeconds 이하이면 선제 갱신
            if (!JwtUtils.TryGetExpiry(tokens.AccessToken, out var exp))
            {
                // 만료 정보를 알 수 없으면 안전하게 갱신 시도
                return await AuthenticateWithRefreshAsync();
            }

            var nowSeconds = JwtUtils.GetUnixTimeSeconds();
            if (exp - nowSeconds > thresholdSeconds)
            {
                return AuthResult.Ok(tokens.UserId, tokens.Nickname, tokens.Email, null, tokens.AccessToken, tokens.RefreshToken, null);
            }

            // 만료 임박 → refresh + verify
            var result = await backendClient.VerifyAsync(tokens.RefreshToken, deviceId, tokens.AccessToken);
            if (result.Success)
            {
                tokens = new AuthTokens(result.IdToken, result.RefreshToken, result.UserId, result.Nickname, result.Email);
                tokenStorage.SaveTokens(tokens);
            }
            return result;
        }
    }
}
