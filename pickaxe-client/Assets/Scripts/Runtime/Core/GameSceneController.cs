using System;
using System.Threading.Tasks;
using InfinitePickaxe.Client.Auth;
using InfinitePickaxe.Client.Net;
using UnityEngine;
using UnityEngine.SceneManagement;
using Infinitepickaxe;

namespace InfinitePickaxe.Client.Core
{
    /// <summary>
    /// Game 씬 전체를 관리하는 컨트롤러
    /// - 서버 연결
    /// - 핸드셰이크
    /// - 초기 데이터 로드
    /// </summary>
    public class GameSceneController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private string loginSceneName = "Title";
        [SerializeField] private float handshakeTimeoutSeconds = 10f;

        [Header("UI References")]
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private GameObject gameUIRoot;

        private NetworkManager networkManager;
        private MessageHandler messageHandler;
        private AuthSessionService sessionService;

        private bool isHandshakeCompleted = false;
        private bool isHandshakeFailed = false;
        private string jwtToken;

        private void Start()
        {
            if (!TryResolveSession())
            {
                FailAndReturnToTitle("세션 정보를 불러올 수 없습니다. 다시 로그인해주세요.", clearSession: true, disconnect: false, immediate: true);
                return;
            }

            jwtToken = sessionService.Tokens.AccessToken;
            if (string.IsNullOrEmpty(jwtToken))
            {
                FailAndReturnToTitle("액세스 토큰이 없습니다. 다시 로그인해주세요.", clearSession: true, disconnect: false, immediate: true);
                return;
            }

            // 초기 UI 상태
            if (loadingPanel != null)
                loadingPanel.SetActive(true);
            if (gameUIRoot != null)
                gameUIRoot.SetActive(false);

            // NetworkManager와 MessageHandler 초기화
            networkManager = NetworkManager.Instance;
            messageHandler = MessageHandler.Instance;

            // 서버 연결 시작
            _ = ConnectToServerAsync();
        }

        private void OnEnable()
        {
            if (networkManager == null)
            {
                networkManager = NetworkManager.Instance;
            }

            if (messageHandler == null)
            {
                messageHandler = MessageHandler.Instance;
            }

            if (messageHandler != null)
            {
                messageHandler.OnHandshakeResult += HandleHandshakeResult;
                messageHandler.OnErrorNotification += HandleErrorNotification;
            }

            if (networkManager != null)
            {
                networkManager.OnDisconnected += HandleDisconnected;
            }
        }

        private void OnDisable()
        {
            if (messageHandler != null)
            {
                messageHandler.OnHandshakeResult -= HandleHandshakeResult;
                messageHandler.OnErrorNotification -= HandleErrorNotification;
            }

            if (networkManager != null)
            {
                networkManager.OnDisconnected -= HandleDisconnected;
            }
        }

        /// <summary>
        /// 서버에 연결하고 핸드셰이크를 수행합니다
        /// </summary>
        private async Task ConnectToServerAsync()
        {
            Debug.Log("게임 서버 연결 시작...");

            try
            {
                // TCP 연결
                bool connected = await networkManager.ConnectAsync(jwtToken);

                if (!connected)
                {
                    Debug.LogError("서버 연결 실패");
                    FailAndReturnToTitle("서버 연결에 실패했습니다. 다시 시도해주세요.");
                    return;
                }

                Debug.Log("서버 연결 성공. 핸드셰이크 대기 중...");

                // 핸드셰이크 응답 대기 (타임아웃)
                float timeoutTime = Time.time + handshakeTimeoutSeconds;
                while (!isHandshakeCompleted && !isHandshakeFailed && Time.time < timeoutTime)
                {
                    await Task.Delay(100);
                }

                if (isHandshakeCompleted)
                {
                    Debug.Log("핸드셰이크 성공! 게임 UI 활성화");
                    OnGameReady();
                }
                else if (isHandshakeFailed)
                {
                    Debug.LogError("핸드셰이크 실패");
                    FailAndReturnToTitle("인증에 실패했습니다. 다시 로그인해주세요.", clearSession: true);
                }
                else
                {
                    Debug.LogError("핸드셰이크 타임아웃");
                    FailAndReturnToTitle("서버 응답 시간이 초과되었습니다. 다시 시도해주세요.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"서버 연결 중 오류 발생: {ex.Message}\n{ex.StackTrace}");
                FailAndReturnToTitle($"연결 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 핸드셰이크 결과 처리
        /// </summary>
        private void HandleHandshakeResult(HandshakeResponse result)
        {
            if (result.Success)
            {
                Debug.Log($"핸드셰이크 성공: {result.Message}");
                isHandshakeCompleted = true;
            }
            else
            {
                Debug.LogError($"핸드셰이크 실패: {result.Message}");
                isHandshakeFailed = true;
            }
        }

        /// <summary>
        /// 서버 에러 알림 처리
        /// </summary>
        private void HandleErrorNotification(ErrorNotification error)
        {
            Debug.LogError($"서버 에러: [{error.ErrorCode}] {error.Message}");

            // 인증 관련 에러는 로그인 화면으로 돌아가기
            if (error.ErrorCode == "AUTH_INVALID" || error.ErrorCode == "AUTH_EXPIRED")
            {
                FailAndReturnToTitle("세션이 만료되었습니다. 다시 로그인해주세요.", clearSession: true);
            }
        }

        /// <summary>
        /// 서버 연결 끊김 처리
        /// </summary>
        private void HandleDisconnected(string reason)
        {
            Debug.LogWarning($"서버 연결 끊김: {reason}");

            // 게임 진행 중에 연결이 끊긴 경우
            if (isHandshakeCompleted)
            {
                FailAndReturnToTitle("서버와의 연결이 끊어졌습니다. 다시 접속해주세요.");
            }
            else
            {
                // 핸드셰이크 전에 끊긴 경우
                FailAndReturnToTitle("서버 연결이 끊어졌습니다. 다시 시도해주세요.");
            }
        }

        /// <summary>
        /// 게임 준비 완료 (핸드셰이크 성공)
        /// </summary>
        private void OnGameReady()
        {
            // 로딩 패널 숨기기
            if (loadingPanel != null)
                loadingPanel.SetActive(false);

            // 게임 UI 활성화
            if (gameUIRoot != null)
                gameUIRoot.SetActive(true);

            Debug.Log("게임 준비 완료!");
        }

        /// <summary>
        /// 세션/연결 실패 처리
        /// </summary>
        private void FailAndReturnToTitle(string message, bool clearSession = false, bool disconnect = true, bool immediate = true)
        {
            Debug.LogError($"연결 오류: {message}");

            TitleController.SetReconnectNotice(message);

            if (disconnect && networkManager != null && networkManager.IsConnected)
            {
                networkManager.Disconnect();
            }

            if (clearSession)
            {
                sessionService?.Clear();
            }

            if (immediate)
            {
                ReturnToLogin();
            }
            else
            {
                Invoke(nameof(ReturnToLogin), 3f);
            }
        }

        /// <summary>
        /// 로그인 화면으로 돌아가기
        /// </summary>
        private void ReturnToLogin()
        {
            // 네트워크 연결 종료
            if (networkManager != null && networkManager.IsConnected)
            {
                networkManager.Disconnect();
            }

            // 로그인 씬 로드
            SceneManager.LoadScene(loginSceneName);
        }

        #region Unity Editor Helper

#if UNITY_EDITOR
        [ContextMenu("테스트: 로그인 화면으로")]
        private void TestReturnToLogin()
        {
            ReturnToLogin();
        }

        [ContextMenu("테스트: 게임 준비 완료")]
        private void TestGameReady()
        {
            OnGameReady();
        }
#endif

        #endregion

        private bool TryResolveSession()
        {
            if (sessionService != null)
            {
                return true;
            }

            if (ClientRuntime.TryResolve(out AuthSessionService resolved))
            {
                sessionService = resolved;
                return true;
            }

            return false;
        }
    }
}
