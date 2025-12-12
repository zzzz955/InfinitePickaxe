using Infinitepickaxe;

namespace InfinitePickaxe.Client.Core
{
    /// <summary>
    /// 게임 입장 전/초기 스냅샷을 보관하는 단순 상태 컨테이너.
    /// </summary>
    public sealed class GameSessionState
    {
        public HandshakeRes LastHandshake { get; set; }
    }
}
