using System;
using System.Threading;
using System.Threading.Tasks;
using InfinitePickaxe.Client.Config;

namespace InfinitePickaxe.Client.Net
{
    public interface INetworkTransport : IDisposable
    {
        bool IsConnected { get; }
        NetworkEndpoint Endpoint { get; }

        event Action Connected;
        event Action<Exception> Disconnected;
        event Action<ReadOnlyMemory<byte>> FrameReceived;

        Task ConnectAsync(NetworkSettings settings, CancellationToken cancellationToken);
        Task DisconnectAsync(string reason = null);
        Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);
    }
}
