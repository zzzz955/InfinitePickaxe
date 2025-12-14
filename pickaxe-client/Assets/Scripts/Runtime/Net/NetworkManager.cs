using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using UnityEngine;
using Infinitepickaxe;

namespace InfinitePickaxe.Client.Net
{
    /// <summary>
    /// TCP 네트워크 통신을 관리하는 매니저
    /// - 서버 연결/재연결
    /// - 메시지 송수신 (Length-Prefix + Protobuf Envelope)
    /// - 하트비트 자동 전송
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        #region Singleton

        private static NetworkManager instance;
        public static NetworkManager Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("NetworkManager");
                    instance = go.AddComponent<NetworkManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        #endregion

        #region Events

        /// <summary>서버 연결 성공 이벤트</summary>
        public event Action OnConnected;

        /// <summary>서버 연결 끊김 이벤트</summary>
        public event Action<string> OnDisconnected;

        /// <summary>메시지 수신 이벤트 (메인 스레드에서 호출)</summary>
        public event Action<Envelope> OnMessageReceived;

        #endregion

        #region Configuration

        [Header("Server Configuration")]
        [SerializeField] private string serverHost = "localhost";
        [SerializeField] private int serverPort = 10001;

        [Header("Connection Settings")]
        [SerializeField] private int connectTimeoutMs = 5000;
        [SerializeField] private int heartbeatIntervalMs = 30000;
        [SerializeField] private bool autoReconnect = true;
        [SerializeField] private int reconnectDelayMs = 3000;

        #endregion

        #region Private Fields

        private TcpClient tcpClient;
        private NetworkStream networkStream;
        private CancellationTokenSource cancellationTokenSource;

        // 메시지 큐 (Thread-Safe)
        private ConcurrentQueue<Envelope> sendQueue = new ConcurrentQueue<Envelope>();
        private ConcurrentQueue<Envelope> receiveQueue = new ConcurrentQueue<Envelope>();

        // 연결 상태
        private bool isConnected = false;
        private bool isConnecting = false;

        // 하트비트
        private float lastHeartbeatTime;

        #endregion

        #region Public Properties

        public bool IsConnected => isConnected && tcpClient != null && tcpClient.Connected;
        public string ServerHost => serverHost;
        public int ServerPort => serverPort;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            // 메인 스레드 액션 실행
            UnityMainThreadDispatcher.ExecuteAll();

            // 수신 큐에서 메시지를 꺼내 메인 스레드에서 처리
            while (receiveQueue.TryDequeue(out Envelope envelope))
            {
                try
                {
                    OnMessageReceived?.Invoke(envelope);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"메시지 처리 중 오류: {ex.Message}\n{ex.StackTrace}");
                }
            }

            // 하트비트 전송 (연결 상태 유지)
            if (IsConnected && Time.time - lastHeartbeatTime >= heartbeatIntervalMs / 1000f)
            {
                SendHeartbeat();
                lastHeartbeatTime = Time.time;
            }
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        private void OnApplicationQuit()
        {
            Disconnect();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 서버에 연결합니다
        /// </summary>
        public async Task<bool> ConnectAsync(string jwtToken)
        {
            if (isConnecting)
            {
                Debug.LogWarning("이미 연결 시도 중입니다.");
                return false;
            }

            if (IsConnected)
            {
                Debug.LogWarning("이미 서버에 연결되어 있습니다.");
                return true;
            }

            isConnecting = true;

            try
            {
                Debug.Log($"서버 연결 시도: {serverHost}:{serverPort}");

                // TCP 클라이언트 생성
                tcpClient = new TcpClient();
                tcpClient.NoDelay = true; // Nagle 알고리즘 비활성화 (지연 시간 최소화)

                // 연결 시도 (타임아웃 적용)
                var connectTask = tcpClient.ConnectAsync(serverHost, serverPort);
                var timeoutTask = Task.Delay(connectTimeoutMs);

                if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
                {
                    throw new TimeoutException($"서버 연결 시간 초과 ({connectTimeoutMs}ms)");
                }

                await connectTask;

                // 네트워크 스트림 획득
                networkStream = tcpClient.GetStream();
                isConnected = true;

                Debug.Log("서버 연결 성공!");

                // 메시지 송수신 태스크 시작
                cancellationTokenSource = new CancellationTokenSource();
                _ = SendLoopAsync(cancellationTokenSource.Token);
                _ = ReceiveLoopAsync(cancellationTokenSource.Token);

                // 핸드셰이크 전송
                SendHandshake(jwtToken);

                // 연결 성공 이벤트 호출 (메인 스레드)
                UnityMainThreadDispatcher.Enqueue(() => OnConnected?.Invoke());

                isConnecting = false;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"서버 연결 실패: {ex.Message}");
                CleanupConnection();
                isConnecting = false;

                // 자동 재연결
                if (autoReconnect)
                {
                    _ = ReconnectAsync(jwtToken);
                }

                return false;
            }
        }

        /// <summary>
        /// 서버 연결을 끊습니다
        /// </summary>
        public void Disconnect()
        {
            if (!IsConnected && !isConnecting)
            {
                return;
            }

            Debug.Log("서버 연결 종료");
            autoReconnect = false; // 재연결 방지
            CleanupConnection();
        }

        /// <summary>
        /// 메시지를 서버로 전송합니다
        /// </summary>
        public void SendMessage(Envelope envelope)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("서버에 연결되어 있지 않습니다. 메시지를 전송할 수 없습니다.");
                return;
            }

            sendQueue.Enqueue(envelope);
        }

        #endregion

        #region Private Methods - Connection

        private async Task ReconnectAsync(string jwtToken)
        {
            await Task.Delay(reconnectDelayMs);
            Debug.Log("서버 재연결 시도...");
            await ConnectAsync(jwtToken);
        }

        private void CleanupConnection()
        {
            isConnected = false;

            // 취소 토큰 발행
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;

            // 스트림 및 클라이언트 정리
            networkStream?.Close();
            networkStream?.Dispose();
            networkStream = null;

            tcpClient?.Close();
            tcpClient?.Dispose();
            tcpClient = null;

            // 큐 초기화
            while (sendQueue.TryDequeue(out _)) { }
            while (receiveQueue.TryDequeue(out _)) { }

            // 연결 종료 이벤트 호출 (메인 스레드)
            UnityMainThreadDispatcher.Enqueue(() => OnDisconnected?.Invoke("연결 종료"));
        }

        #endregion

        #region Private Methods - Send/Receive

        /// <summary>
        /// 송신 루프 (별도 스레드)
        /// </summary>
        private async Task SendLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && IsConnected)
                {
                    if (sendQueue.TryDequeue(out Envelope envelope))
                    {
                        await SendEnvelopeAsync(envelope, cancellationToken);
                    }
                    else
                    {
                        await Task.Delay(10, cancellationToken); // CPU 사용률 감소
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("송신 루프 취소됨");
            }
            catch (Exception ex)
            {
                Debug.LogError($"송신 루프 오류: {ex.Message}");
                CleanupConnection();
            }
        }

        /// <summary>
        /// 수신 루프 (별도 스레드)
        /// </summary>
        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && IsConnected)
                {
                    Envelope envelope = await ReceiveEnvelopeAsync(cancellationToken);
                    if (envelope != null)
                    {
                        receiveQueue.Enqueue(envelope);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("수신 루프 취소됨");
            }
            catch (Exception ex)
            {
                Debug.LogError($"수신 루프 오류: {ex.Message}");
                CleanupConnection();
            }
        }

        /// <summary>
        /// Envelope 메시지를 전송합니다 (Length-Prefix 포함)
        /// </summary>
        private async Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken)
        {
            try
            {
                // Protobuf 직렬화
                byte[] messageBytes = envelope.ToByteArray();
                int messageLength = messageBytes.Length;

                // Length-Prefix (4바이트, Little-Endian 고정)
                byte[] lengthBytes = BitConverter.GetBytes(messageLength);

                // Length + Message 전송
                if (!IsConnected || networkStream == null)
                {
                    Debug.LogWarning("전송 스트림이 준비되지 않아 메시지 전송을 건너뜁니다.");
                    return;
                }

                await networkStream.WriteAsync(lengthBytes, 0, 4, cancellationToken);
                await networkStream.WriteAsync(messageBytes, 0, messageLength, cancellationToken);
                await networkStream.FlushAsync(cancellationToken);

#if UNITY_EDITOR || DEBUG_NET
                Debug.Log($"메시지 전송: {envelope.Type} ({messageLength} bytes)");
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"메시지 전송 실패: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Envelope 메시지를 수신합니다 (Length-Prefix 포함)
        /// </summary>
        private async Task<Envelope> ReceiveEnvelopeAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (networkStream == null)
                {
                    throw new IOException("수신 스트림이 준비되지 않았습니다.");
                }

                // Length-Prefix 수신 (4바이트, Little-Endian 고정)
                byte[] lengthBytes = new byte[4];
                int bytesRead = 0;
                while (bytesRead < 4)
                {
                    int read = await networkStream.ReadAsync(lengthBytes, bytesRead, 4 - bytesRead, cancellationToken);
                    if (read == 0)
                    {
                        throw new IOException("서버 연결이 끊어졌습니다 (Length 수신 실패)");
                    }
                    bytesRead += read;
                }

                int messageLength = BitConverter.ToInt32(lengthBytes, 0);

                // 메시지 크기 검증
                if (messageLength <= 0 || messageLength > 10 * 1024 * 1024) // 10MB 제한
                {
                    throw new InvalidDataException($"비정상적인 메시지 크기: {messageLength} bytes");
                }

                // Message 수신
                byte[] messageBytes = new byte[messageLength];
                bytesRead = 0;
                while (bytesRead < messageLength)
                {
                    int read = await networkStream.ReadAsync(messageBytes, bytesRead, messageLength - bytesRead, cancellationToken);
                    if (read == 0)
                    {
                        throw new IOException("서버 연결이 끊어졌습니다 (Message 수신 실패)");
                    }
                    bytesRead += read;
                }

                // Protobuf 역직렬화
                Envelope envelope = Envelope.Parser.ParseFrom(messageBytes);

#if UNITY_EDITOR || DEBUG_NET
                if (envelope.Type != MessageType.MiningUpdate)
                {
                    Debug.Log($"메시지 수신: {envelope.Type} ({messageLength} bytes)");
                }
#endif

                return envelope;
            }
            catch (Exception ex)
            {
                Debug.LogError($"메시지 수신 실패: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Private Methods - Protocol Messages

        /// <summary>
        /// 핸드셰이크 메시지 전송
        /// </summary>
        private void SendHandshake(string jwtToken)
        {
            var handshake = new HandshakeRequest
            {
                Jwt = jwtToken,
                ClientVersion = Application.version,
                DeviceId = SystemInfo.deviceUniqueIdentifier
            };

            var envelope = new Envelope
            {
                Type = MessageType.Handshake,
                Handshake = handshake
            };

            SendMessage(envelope);
            Debug.Log("핸드셰이크 전송");
        }

        /// <summary>
        /// 하트비트 메시지 전송
        /// </summary>
        private void SendHeartbeat()
        {
            var heartbeat = new Heartbeat
            {
                ClientTimeMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            var envelope = new Envelope
            {
                Type = MessageType.Heartbeat,
                Heartbeat = heartbeat
            };

            SendMessage(envelope);

#if UNITY_EDITOR || DEBUG_NET
            Debug.Log("하트비트 전송");
#endif
        }

        #endregion
    }

    /// <summary>
    /// Unity 메인 스레드에서 액션을 실행하기 위한 디스패처
    /// </summary>
    public static class UnityMainThreadDispatcher
    {
        private static readonly ConcurrentQueue<Action> executionQueue = new ConcurrentQueue<Action>();

        /// <summary>
        /// 메인 스레드에서 실행할 액션을 큐에 추가합니다
        /// </summary>
        public static void Enqueue(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            executionQueue.Enqueue(action);
        }

        /// <summary>
        /// 큐에 있는 모든 액션을 메인 스레드에서 실행합니다
        /// NetworkManager.Update()에서 호출됩니다
        /// </summary>
        public static void ExecuteAll()
        {
            while (executionQueue.TryDequeue(out Action action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"메인 스레드 액션 실행 오류: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }
    }
}
