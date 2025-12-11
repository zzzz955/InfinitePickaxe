using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using InfinitePickaxe.Client.Config;
using UnityEngine;

namespace InfinitePickaxe.Client.Net
{
    public sealed class LengthPrefixedTcpTransport : INetworkTransport
    {
        private readonly SemaphoreSlim connectLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);

        private TcpClient tcpClient;
        private Stream stream;
        private CancellationTokenSource receiveLoopCts;
        private Task receiveLoopTask;
        private NetworkSettings settings;
        private bool disposed;

        public bool IsConnected => stream != null && tcpClient != null && tcpClient.Connected;
        public NetworkEndpoint Endpoint { get; private set; }

        public event Action Connected;
        public event Action<Exception> Disconnected;
        public event Action<ReadOnlyMemory<byte>> FrameReceived;

        public async Task ConnectAsync(NetworkSettings networkSettings, CancellationToken cancellationToken)
        {
            if (networkSettings == null)
            {
                throw new ArgumentNullException(nameof(networkSettings));
            }

            await connectLock.WaitAsync(cancellationToken);
            try
            {
                ThrowIfDisposed();
                await DisconnectInternalAsync("reconnect");

                settings = networkSettings;
                Endpoint = networkSettings.Endpoint;

                tcpClient = new TcpClient
                {
                    NoDelay = true,
                    ReceiveBufferSize = networkSettings.MaxMessageBytes,
                    SendTimeout = (int)Math.Ceiling(networkSettings.SendTimeout.TotalMilliseconds),
                    ReceiveTimeout = (int)Math.Ceiling(networkSettings.ReceiveTimeout.TotalMilliseconds)
                };

                var connectTask = tcpClient.ConnectAsync(Endpoint.Host, Endpoint.Port);
                var timeoutTask = Task.Delay(networkSettings.ConnectTimeout);
                var cancelTask = Task.Delay(Timeout.Infinite, cancellationToken);

                var completed = await Task.WhenAny(connectTask, timeoutTask, cancelTask);
                if (completed == cancelTask)
                {
                    tcpClient.Close();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (completed == timeoutTask)
                {
                    tcpClient.Close();
                    throw new TimeoutException($"Connect to {Endpoint} timed out after {networkSettings.ConnectTimeout.TotalSeconds:0.#}s");
                }

                await connectTask;

                stream = tcpClient.GetStream();
                if (Endpoint.UseTls)
                {
                    var sslStream = new SslStream(stream, false);
                    await sslStream.AuthenticateAsClientAsync(Endpoint.Host);
                    stream = sslStream;
                }

                receiveLoopCts = new CancellationTokenSource();
                receiveLoopTask = ReceiveLoopAsync(receiveLoopCts.Token);
                Connected?.Invoke();
                Debug.Log($"Transport connected to {Endpoint}.");
            }
            finally
            {
                connectLock.Release();
            }
        }

        public async Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (!IsConnected)
            {
                throw new InvalidOperationException("Transport is not connected.");
            }

            if (payload.Length == 0)
            {
                return;
            }

            if (payload.Length > settings.MaxMessageBytes)
            {
                throw new InvalidDataException($"Payload length {payload.Length} exceeds limit {settings.MaxMessageBytes}.");
            }

            await sendLock.WaitAsync(cancellationToken);
            try
            {
                var currentStream = stream;
                if (currentStream == null)
                {
                    throw new InvalidOperationException("Transport is not connected.");
                }

                using (var timeoutCts = new CancellationTokenSource(settings.SendTimeout))
                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token))
                {
                    var prefix = BitConverter.GetBytes(payload.Length);
                    await currentStream.WriteAsync(prefix, 0, prefix.Length, linkedCts.Token);

                    var buffer = payload.ToArray();
                    await currentStream.WriteAsync(buffer, 0, buffer.Length, linkedCts.Token);
                    await currentStream.FlushAsync(linkedCts.Token);
                }
            }
            finally
            {
                sendLock.Release();
            }
        }

        public async Task DisconnectAsync(string reason = null)
        {
            var shouldNotify = IsConnected || stream != null || tcpClient != null;
            await connectLock.WaitAsync();
            try
            {
                await DisconnectInternalAsync(reason);
            }
            finally
            {
                connectLock.Release();
            }

            if (shouldNotify && reason != "receive error")
            {
                Disconnected?.Invoke(null);
            }
        }

        private Task DisconnectInternalAsync(string reason)
        {
            var receiveTask = receiveLoopTask;
            receiveLoopTask = null;

            if (receiveLoopCts != null)
            {
                receiveLoopCts.Cancel();
                receiveLoopCts.Dispose();
                receiveLoopCts = null;
            }

            if (receiveTask != null)
            {
                _ = receiveTask.ContinueWith(t =>
                {
                    if (t.IsFaulted && t.Exception != null)
                    {
                        Debug.LogWarning($"Receive loop ended with error: {t.Exception}");
                    }
                }, TaskScheduler.Default);
            }

            stream?.Dispose();
            stream = null;

            tcpClient?.Close();
            tcpClient?.Dispose();
            tcpClient = null;

            if (!string.IsNullOrEmpty(reason))
            {
                Debug.Log($"Transport disconnected ({reason}).");
            }

            return Task.CompletedTask;
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var lengthBuffer = new byte[4];

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await ReadExactlyAsync(lengthBuffer, 4, cancellationToken);
                    var length = BitConverter.ToInt32(lengthBuffer, 0);
                    if (length <= 0 || length > settings.MaxMessageBytes)
                    {
                        throw new InvalidDataException($"Invalid frame length {length}.");
                    }

                    var payload = new byte[length];
                    await ReadExactlyAsync(payload, length, cancellationToken);

                    FrameReceived?.Invoke(new ReadOnlyMemory<byte>(payload));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Disconnected?.Invoke(ex);
                await DisconnectInternalAsync("receive error");
            }
        }

        private async Task ReadExactlyAsync(byte[] buffer, int count, CancellationToken cancellationToken)
        {
            var currentStream = stream;
            if (currentStream == null)
            {
                throw new IOException("Stream is not available.");
            }

            var offset = 0;
            while (offset < count)
            {
                var read = await currentStream.ReadAsync(buffer, offset, count - offset, cancellationToken);
                if (read == 0)
                {
                    throw new IOException("Remote peer closed the connection.");
                }

                offset += read;
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(LengthPrefixedTcpTransport));
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            try
            {
                DisconnectAsync("dispose").GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Dispose failed: {ex}");
            }

            sendLock.Dispose();
            connectLock.Dispose();
        }
    }
}
