namespace InfinitePickaxe.Client.Auth
{
    public interface ITokenStorage
    {
        bool HasRefreshToken();
        string GetRefreshToken();
        void SaveTokens(AuthTokens tokens);
        void Clear();
    }
}
