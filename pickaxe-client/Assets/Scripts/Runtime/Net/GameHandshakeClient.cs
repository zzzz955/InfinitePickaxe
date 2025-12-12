using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using InfinitePickaxe.Client.Auth;
using InfinitePickaxe.Client.Config;
using InfinitePickaxe.Client.Core;
using InfinitePickaxe.Client.Net;
using UnityEngine;

namespace InfinitePickaxe.Client.Net
{
    public sealed class GameHandshakeResult
    {
        public bool Success { get; }
        public string Error { get; }
        public Infinitepickaxe.HandshakeRes Response { get; }

        private GameHandshakeResult(bool success, string error, Infinitepickaxe.HandshakeRes res)
        {
            Success = success;
            Error = error;
            Response = res;
        }

        public static GameHandshakeResult Ok(Infinitepickaxe.HandshakeRes res) => new GameHandshakeResult(true, null, res);
        public static GameHandshakeResult Fail(string error) => new GameHandshakeResult(false, error, null);
    }

    /// <summary>
    /// TCP 연결 + HANDSHAKE 송수신을 처리하는 클라이언트.
    /// </summary>
    public sealed class GameHandshakeClient : IDisposable
    {
        private readonly NetworkClient networkClient;
        private readonly string clientVersion;
        private bool disposed;

        public GameHandshakeClient(NetworkClient client, NetworkSettings settings, string clientVersion)
        {
            networkClient = client ?? throw new ArgumentNullException(nameof(client));
            this.clientVersion = string.IsNullOrWhiteSpace(clientVersion) ? Application.version : clientVersion;
        }

        public async Task<GameHandshakeResult> ConnectAndHandshakeAsync(string jwt, string deviceId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(jwt))
            {
                return GameHandshakeResult.Fail("JWT가 없습니다.");
            }

            ThrowIfDisposed();

            TaskCompletionSource<GameHandshakeResult> tcs = new TaskCompletionSource<GameHandshakeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            void FrameHandler(ReadOnlyMemory<byte> frame)
            {
                try
                {
                    var env = Infinitepickaxe.Envelope.Parser.ParseFrom(frame.Span);
                    switch (env.MsgType)
                    {
                        case "HANDSHAKE_RES":
                            var res = Infinitepickaxe.HandshakeRes.Parser.ParseFrom(env.Payload);
                            tcs.TrySetResult(res.Ok
                                ? GameHandshakeResult.Ok(res)
                                : GameHandshakeResult.Fail(string.IsNullOrEmpty(res.Error) ? "HANDSHAKE_FAILED" : res.Error));
                            break;
                        case "ERROR":
                            var err = Infinitepickaxe.Error.Parser.ParseFrom(env.Payload);
                            var code = string.IsNullOrEmpty(err.ErrorCode) ? "ERROR" : err.ErrorCode;
                            tcs.TrySetResult(GameHandshakeResult.Fail($"{code}: {err.ErrorMessage}"));
                            break;
                        default:
                            // ignore other messages during handshake
                            break;
                    }
                }
                catch (Exception ex)
                {
                    tcs.TrySetResult(GameHandshakeResult.Fail($"HANDSHAKE_PARSE_ERROR: {ex.Message}"));
                }
            }

            networkClient.FrameReceived += FrameHandler;
            try
            {
                await networkClient.ConnectAsync(cancellationToken);

                var req = new Infinitepickaxe.HandshakeReq
                {
                    Jwt = jwt,
                    DeviceId = deviceId ?? string.Empty,
                    ClientVersion = clientVersion
                };

                var env = new Infinitepickaxe.Envelope
                {
                    MsgType = "HANDSHAKE",
                    Version = 1,
                    Seq = 1,
                    Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Payload = req.ToByteString()
                };

                var payload = env.ToByteArray();
                await networkClient.SendAsync(payload, cancellationToken);

                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, linked.Token));
                if (completed != tcs.Task)
                {
                    return GameHandshakeResult.Fail("HANDSHAKE_TIMEOUT");
                }
                return await tcs.Task;
            }
            finally
            {
                networkClient.FrameReceived -= FrameHandler;
                // 연결은 호출자에게 맡김 (성공 시 유지, 실패 시 종료)
            }
        }

        public void Dispose()
        {
            disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(GameHandshakeClient));
            }
        }
    }
}
