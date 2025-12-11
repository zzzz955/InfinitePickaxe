using System;
using System.Threading;
using System.Threading.Tasks;
using InfinitePickaxe.Client.Config;

namespace InfinitePickaxe.Client.Net
{
    public sealed class NetworkClient : IDisposable
    {
        private readonly INetworkTransport transport;
        private readonly NetworkSettings settings;
        private bool disposed;

        public bool IsConnected => transport.IsConnected;
        public NetworkEndpoint Endpoint => transport.Endpoint;

        public event Action Connected
        {
            add => transport.Connected += value;
            remove => transport.Connected -= value;
        }

        public event Action<Exception> Disconnected
        {
            add => transport.Disconnected += value;
            remove => transport.Disconnected -= value;
        }

        public event Action<ReadOnlyMemory<byte>> FrameReceived
        {
            add => transport.FrameReceived += value;
            remove => transport.FrameReceived -= value;
        }

        public NetworkClient(INetworkTransport transport, NetworkSettings settings)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return transport.ConnectAsync(settings, cancellationToken);
        }

        public Task DisconnectAsync(string reason = null)
        {
            ThrowIfDisposed();
            return transport.DisconnectAsync(reason);
        }

        public Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return transport.SendAsync(payload, cancellationToken);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            transport.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(NetworkClient));
            }
        }
    }
}
