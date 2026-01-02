using System;
using System.Collections;
using System.Threading.Tasks;
using InfinitePickaxe.Client.Auth;
using InfinitePickaxe.Client.Net;
using InfinitePickaxe.Client.UI.Common;
using InfinitePickaxe.Client.Metadata;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Infinitepickaxe;
using TMPro;

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
        [SerializeField] private float snapshotTimeoutSeconds = 10f;
        [SerializeField] private float reconnectOverlayDelaySeconds = 0.5f;

        [Header("UI References")]
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private GameObject gameUIRoot;

        [Header("Game Exit Modal")]
        [SerializeField] private GameObject gameExitModal;
        [SerializeField] private Button gameExitConfirmButton;
        [SerializeField] private Button gameExitOfflineModeButton;
        [SerializeField] private Button gameExitCancelButton;

        [Header("Offline Mode Detail Modal")]
        [SerializeField] private GameObject offlineModeDetailModal;
        [SerializeField] private TextMeshProUGUI offlineModeRemainingText;
        [SerializeField] private TextMeshProUGUI offlineModeRewardText;
        [SerializeField] private TextMeshProUGUI offlineModeMiningCountText;
        [SerializeField] private TextMeshProUGUI offlineModeNoticeText;
        [SerializeField] private Button offlineModeDetailConfirmButton;
        [SerializeField] private Button offlineModeDetailCancelButton;

        [Header("Offline Reward Modal")]
        [SerializeField] private GameObject offlineRewardModal;
        [SerializeField] private TextMeshProUGUI offlineRewardMessageText;
        [SerializeField] private Button offlineRewardCloseButton;

        private NetworkManager networkManager;
        private MessageHandler messageHandler;
        private AuthSessionService sessionService;

        private bool isHandshakeCompleted = false;
        private bool isHandshakeFailed = false;
        private bool isSnapshotReceived = false;
        private bool isGameReady = false;
        private bool overlayOwned = false;
        private Coroutine reconnectOverlayCoroutine;
        private bool reconnectOverlayPending = false;
        private string jwtToken;
        private bool offlineModeRequestPending = false;
        private bool suppressDisconnectNotice = false;
        private MineralMetaResolver mineralMetaResolver;

        public GameObject LoadingPanel => loadingPanel;

        public void SetLoadingVisible(bool visible)
        {
            SetLocalLoadingVisible(visible);
        }

        private void ShowLoadingOverlay(string message)
        {
            var manager = LoadingOverlayManager.Instance;
            if (manager != null)
            {
                if (!overlayOwned)
                {
                    if (!manager.IsVisible)
                    {
                        manager.Show(message);
                    }
                    else if (!string.IsNullOrEmpty(message))
                    {
                        manager.SetMessage(message);
                    }
                    overlayOwned = true;
                }
                else if (!string.IsNullOrEmpty(message))
                {
                    manager.SetMessage(message);
                }

                SetLocalLoadingVisible(false);
                return;
            }

            SetLocalLoadingVisible(true);
        }

        private void HideLoadingOverlay()
        {
            var manager = LoadingOverlayManager.Instance;
            if (manager != null)
            {
                if (overlayOwned)
                {
                    manager.Hide();
                    overlayOwned = false;
                }
                SetLocalLoadingVisible(false);
                return;
            }

            SetLocalLoadingVisible(false);
        }

        private void StartReconnectOverlayDelay()
        {
            reconnectOverlayPending = true;
            if (reconnectOverlayCoroutine != null)
            {
                StopCoroutine(reconnectOverlayCoroutine);
            }
            reconnectOverlayCoroutine = StartCoroutine(ShowReconnectOverlayDelayed());
        }

        private void CancelReconnectOverlay()
        {
            reconnectOverlayPending = false;
            if (reconnectOverlayCoroutine != null)
            {
                StopCoroutine(reconnectOverlayCoroutine);
                reconnectOverlayCoroutine = null;
            }
        }

        private IEnumerator ShowReconnectOverlayDelayed()
        {
            if (reconnectOverlayDelaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(reconnectOverlayDelaySeconds);
            }

            if (!reconnectOverlayPending)
            {
                yield break;
            }

            if (networkManager != null && networkManager.IsConnected)
            {
                yield break;
            }

            ShowLoadingOverlay("\uC7AC\uC5F0\uACB0 \uC911...");
        }

        private void SetLocalLoadingVisible(bool visible)
        {
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(visible);
            }
        }

        private void SetupGameExitModalButtons()
        {
            if (gameExitModal == null) return;

            var backgroundButton = gameExitModal.GetComponent<Button>();
            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveAllListeners();
                backgroundButton.onClick.AddListener(CloseGameExitModal);
            }

            var modalPanel = gameExitModal.transform.Find("ModalPanel");
            if (modalPanel != null)
            {
                var panelButton = modalPanel.GetComponent<Button>();
                if (panelButton == null)
                {
                    panelButton = modalPanel.gameObject.AddComponent<Button>();
                    panelButton.transition = Selectable.Transition.None;
                }
                panelButton.onClick.RemoveAllListeners();
            }

            if (gameExitCancelButton != null)
            {
                gameExitCancelButton.onClick.RemoveAllListeners();
                gameExitCancelButton.onClick.AddListener(CloseGameExitModal);
            }

            if (gameExitConfirmButton != null)
            {
                gameExitConfirmButton.onClick.RemoveAllListeners();
                gameExitConfirmButton.onClick.AddListener(ConfirmExitGame);
            }

            if (gameExitOfflineModeButton != null)
            {
                gameExitOfflineModeButton.onClick.RemoveAllListeners();
                gameExitOfflineModeButton.onClick.AddListener(OpenOfflineModeDetailModal);
            }
        }

        private void SetupOfflineModeDetailModalButtons()
        {
            if (offlineModeDetailModal == null) return;

            var backgroundButton = offlineModeDetailModal.GetComponent<Button>();
            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveAllListeners();
                backgroundButton.onClick.AddListener(CloseOfflineModeDetailModal);
            }

            var modalPanel = offlineModeDetailModal.transform.Find("ModalPanel");
            if (modalPanel != null)
            {
                var panelButton = modalPanel.GetComponent<Button>();
                if (panelButton == null)
                {
                    panelButton = modalPanel.gameObject.AddComponent<Button>();
                    panelButton.transition = Selectable.Transition.None;
                }
                panelButton.onClick.RemoveAllListeners();
            }

            if (offlineModeDetailCancelButton != null)
            {
                offlineModeDetailCancelButton.onClick.RemoveAllListeners();
                offlineModeDetailCancelButton.onClick.AddListener(CloseOfflineModeDetailModal);
            }

            if (offlineModeDetailConfirmButton != null)
            {
                offlineModeDetailConfirmButton.onClick.RemoveAllListeners();
                offlineModeDetailConfirmButton.onClick.AddListener(ConfirmOfflineModeStart);
            }
        }

        private void SetupOfflineRewardModalButtons()
        {
            if (offlineRewardModal == null) return;

            var backgroundButton = offlineRewardModal.GetComponent<Button>();
            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveAllListeners();
                backgroundButton.onClick.AddListener(CloseOfflineRewardModal);
            }

            var modalPanel = offlineRewardModal.transform.Find("ModalPanel");
            if (modalPanel != null)
            {
                var panelButton = modalPanel.GetComponent<Button>();
                if (panelButton == null)
                {
                    panelButton = modalPanel.gameObject.AddComponent<Button>();
                    panelButton.transition = Selectable.Transition.None;
                }
                panelButton.onClick.RemoveAllListeners();
            }

            if (offlineRewardCloseButton != null)
            {
                offlineRewardCloseButton.onClick.RemoveAllListeners();
                offlineRewardCloseButton.onClick.AddListener(CloseOfflineRewardModal);
            }
        }

        public void RequestExitWithOfflineMode()
        {
            RequestExitFromGame();
        }

        public void RequestExitFromGame()
        {
            OpenGameExitModal();
        }

        private void OpenGameExitModal()
        {
            if (gameExitModal == null)
            {
                ConfirmExitGame();
                return;
            }

            if (gameExitModal.activeSelf)
            {
                return;
            }

            gameExitModal.SetActive(true);
            gameExitModal.transform.SetAsLastSibling();
        }

        private void CloseGameExitModal()
        {
            if (gameExitModal != null)
            {
                gameExitModal.SetActive(false);
            }
        }

        private void OpenOfflineModeDetailModal()
        {
            if (offlineModeDetailModal == null)
            {
                ConfirmOfflineModeStart();
                return;
            }

            CloseGameExitModal();
            UpdateOfflineModeDetailUI();
            offlineModeDetailModal.SetActive(true);
            offlineModeDetailModal.transform.SetAsLastSibling();
        }

        private void CloseOfflineModeDetailModal()
        {
            if (offlineModeDetailModal != null)
            {
                offlineModeDetailModal.SetActive(false);
            }
        }

        private void ConfirmExitGame()
        {
            CloseGameExitModal();
            CloseOfflineModeDetailModal();
            ExitGameImmediate();
        }

        private void CloseOfflineRewardModal()
        {
            if (offlineRewardModal != null)
            {
                offlineRewardModal.SetActive(false);
            }
        }

        private void ConfirmOfflineModeStart()
        {
            if (offlineModeRequestPending)
            {
                return;
            }

            if (!TryBuildOfflineEstimate(out var estimate, out var errorCode))
            {
                UpdateOfflineModeDetailUI(estimate);
                SetOfflineModeNotice(errorCode);
                if (offlineModeDetailConfirmButton != null)
                {
                    offlineModeDetailConfirmButton.interactable = false;
                }
                if (offlineModeDetailModal != null && !offlineModeDetailModal.activeSelf)
                {
                    offlineModeDetailModal.SetActive(true);
                    offlineModeDetailModal.transform.SetAsLastSibling();
                }
                return;
            }

            if (messageHandler == null || networkManager == null || !networkManager.IsConnected)
            {
                SetOfflineModeNotice("NETWORK_NOT_CONNECTED");
                return;
            }

            ClearOfflineModeNotice();
            offlineModeRequestPending = true;
            if (offlineModeDetailConfirmButton != null)
            {
                offlineModeDetailConfirmButton.interactable = false;
            }
            CloseGameExitModal();
            CloseOfflineModeDetailModal();
            ShowLoadingOverlay(string.Empty);
            messageHandler.RequestOfflineModeStart();
        }

        private struct OfflineEstimate
        {
            public uint RemainingSeconds;
            public ulong EstimatedGold;
            public uint MiningCount;
        }

        private bool TryBuildOfflineEstimate(out OfflineEstimate estimate, out string errorCode)
        {
            estimate = default;
            errorCode = string.Empty;

            if (messageHandler == null || !messageHandler.TryGetLastSnapshot(out var snapshot) || snapshot == null)
            {
                errorCode = "SNAPSHOT_MISSING";
                return false;
            }

            if (snapshot.CurrentOfflineHours == 0)
            {
                errorCode = "NO_OFFLINE_TIME";
                return false;
            }

            if (!snapshot.CurrentMineralId.HasValue || snapshot.CurrentMineralId.Value == 0)
            {
                errorCode = "MINERAL_NOT_SELECTED";
                return false;
            }

            if (PickaxeStateCache.Instance.TotalDps == 0)
            {
                errorCode = "DPS_ZERO";
                return false;
            }

            if (mineralMetaResolver == null)
            {
                mineralMetaResolver = new MineralMetaResolver();
            }

            if (!mineralMetaResolver.TryGetMineral(snapshot.CurrentMineralId.Value, out var mineral))
            {
                errorCode = "MINERAL_META_MISSING";
                return false;
            }

            uint availableSeconds = snapshot.CurrentOfflineHours * 3600;
            ulong currentHp = snapshot.MineralHp ?? 0;
            estimate = EstimateOfflineReward(availableSeconds, mineral, PickaxeStateCache.Instance.TotalDps, currentHp);
            return true;
        }

        private OfflineEstimate EstimateOfflineReward(uint availableSeconds, MineralMeta mineral, ulong totalDps, ulong currentHp)
        {
            var estimate = new OfflineEstimate
            {
                RemainingSeconds = availableSeconds,
                EstimatedGold = 0,
                MiningCount = 0
            };

            if (availableSeconds == 0 || mineral == null || mineral.Hp <= 0f || totalDps == 0)
            {
                return estimate;
            }

            double dps = totalDps;
            double maxHp = mineral.Hp;
            double reward = mineral.Gold;
            double respawn = mineral.RespawnSeconds;

            double hp = currentHp > 0 ? Math.Min(currentHp, (ulong)maxHp) : maxHp;
            double remaining = availableSeconds;
            double timeToKill = hp / dps;

            if (remaining < timeToKill)
            {
                return estimate;
            }

            remaining -= timeToKill;
            estimate.MiningCount = 1;
            ulong rewardPer = (ulong)Math.Max(0.0, Math.Round(reward));
            estimate.EstimatedGold += rewardPer;

            double fullKillTime = maxHp / dps;
            double cycleTime = fullKillTime + respawn;
            if (cycleTime > 0 && remaining >= cycleTime)
            {
                ulong cycles = (ulong)Math.Floor(remaining / cycleTime);
                ulong newCount = estimate.MiningCount + cycles;
                estimate.MiningCount = (uint)Math.Min((ulong)uint.MaxValue, newCount);
                estimate.EstimatedGold += cycles * rewardPer;
                remaining -= cycles * cycleTime;
            }

            if (respawn > 0)
            {
                if (remaining < respawn)
                {
                    return estimate;
                }
                remaining -= respawn;
            }

            if (remaining >= fullKillTime)
            {
                estimate.MiningCount = (uint)Math.Min((ulong)uint.MaxValue, (ulong)estimate.MiningCount + 1);
                estimate.EstimatedGold += rewardPer;
            }

            return estimate;
        }

        private void UpdateOfflineModeDetailUI()
        {
            if (TryBuildOfflineEstimate(out var estimate, out var errorCode))
            {
                UpdateOfflineModeDetailUI(estimate);
                ClearOfflineModeNotice();
                if (offlineModeDetailConfirmButton != null)
                {
                    offlineModeDetailConfirmButton.interactable = true;
                }
            }
            else
            {
                UpdateOfflineModeDetailUI(estimate);
                SetOfflineModeNotice(errorCode);
                if (offlineModeDetailConfirmButton != null)
                {
                    offlineModeDetailConfirmButton.interactable = false;
                }
            }
        }

        private void UpdateOfflineModeDetailUI(OfflineEstimate estimate)
        {
            if (offlineModeRemainingText != null)
            {
                offlineModeRemainingText.text = FormatSeconds(estimate.RemainingSeconds);
            }

            if (offlineModeRewardText != null)
            {
                offlineModeRewardText.text = estimate.EstimatedGold.ToString("N0");
            }

            if (offlineModeMiningCountText != null)
            {
                offlineModeMiningCountText.text = estimate.MiningCount.ToString();
            }
        }

        private static string GetOfflineModeNoticeMessage(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return string.Empty;
            }

            return code switch
            {
                "NO_OFFLINE_TIME" => "\uBE44\uC811\uC18D \uC2DC\uAC04\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.",
                "MINERAL_NOT_SELECTED" => "\uC120\uD0DD\uB41C \uAD11\uBB3C\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.",
                "INVALID_MINERAL" => "\uAD11\uBB3C \uC815\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.",
                "MINERAL_META_MISSING" => "\uAD11\uBB3C \uBA54\uD0C0\uB370\uC774\uD130\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.",
                "DPS_ZERO" => "\uD604\uC7AC DPS\uAC00 0\uC785\uB2C8\uB2E4.",
                "SNAPSHOT_MISSING" => "\uC720\uC800 \uB370\uC774\uD130\uAC00 \uC544\uC9C1 \uB85C\uB4DC\uB418\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4.",
                "NETWORK_NOT_CONNECTED" => "\uB124\uD2B8\uC6CC\uD06C \uC5F0\uACB0\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.",
                "REDIS_ERROR" => "\uBE44\uC811\uC18D \uBAA8\uB4DC \uC2DC\uC791\uC5D0 \uC2E4\uD328\uD588\uC2B5\uB2C8\uB2E4.",
                _ => "\uBE44\uC811\uC18D \uBAA8\uB4DC \uC2DC\uC791\uC5D0 \uC2E4\uD328\uD588\uC2B5\uB2C8\uB2E4."
            };
        }

        private void SetOfflineModeNotice(string code)
        {
            if (offlineModeNoticeText == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(code))
            {
                offlineModeNoticeText.text = string.Empty;
                offlineModeNoticeText.gameObject.SetActive(false);
                return;
            }

            var message = GetOfflineModeNoticeMessage(code);
            offlineModeNoticeText.text = string.IsNullOrEmpty(message) ? code : message;
            offlineModeNoticeText.gameObject.SetActive(true);
        }

        private void ClearOfflineModeNotice()
        {
            if (offlineModeNoticeText == null)
            {
                return;
            }

            offlineModeNoticeText.text = string.Empty;
            offlineModeNoticeText.gameObject.SetActive(false);
        }

        private static string FormatSeconds(uint seconds)
        {
            var span = TimeSpan.FromSeconds(seconds);
            return string.Format("{0:00}:{1:00}:{2:00}", (int)span.TotalHours, span.Minutes, span.Seconds);
        }

        private void ExitGameImmediate()
        {
            suppressDisconnectNotice = true;
            if (networkManager != null && networkManager.IsConnected)
            {
                networkManager.Disconnect();
            }
            Application.Quit();
        }

        private void HandleOfflineModeStartResult(OfflineModeStartResult result)
        {
            offlineModeRequestPending = false;
            HideLoadingOverlay();

            if (offlineModeDetailConfirmButton != null)
            {
                offlineModeDetailConfirmButton.interactable = true;
            }

            if (result == null)
            {
                return;
            }

            if (!result.Success)
            {
                UpdateOfflineModeDetailUI();
                SetOfflineModeNotice(result.ErrorCode);
                if (offlineModeDetailModal != null && !offlineModeDetailModal.activeSelf)
                {
                    offlineModeDetailModal.SetActive(true);
                    offlineModeDetailModal.transform.SetAsLastSibling();
                }
                return;
            }

            ExitGameImmediate();
        }

        private void HandleOfflineRewardResult(OfflineRewardResult result)
        {
            if (result == null)
            {
                return;
            }

            if (result.ElapsedSeconds == 0 && result.GoldEarned == 0 && result.MiningCount == 0)
            {
                return;
            }

            ShowOfflineRewardModal(result);
        }

        private void ShowOfflineRewardModal(OfflineRewardResult result)
        {
            if (offlineRewardModal == null || offlineRewardMessageText == null)
            {
                return;
            }

            var elapsed = TimeSpan.FromSeconds(result.ElapsedSeconds);
            string timeText;
            if (elapsed.TotalHours >= 1)
            {
                timeText = string.Format("{0}\uC2DC\uAC04 {1}\uBD84", (int)elapsed.TotalHours, elapsed.Minutes);
            }
            else
            {
                timeText = string.Format("{0}\uBD84 {1}\uCD08", elapsed.Minutes, elapsed.Seconds);
            }

            offlineRewardMessageText.text = string.Format(
                "\uBE44\uC811\uC18D \uCC44\uAD74 \uACB0\uACFC\n\uACBD\uACFC {0}\n\uCC44\uAD74 \uD69F\uC218 {1}\n\uD68D\uB4DD \uACE8\uB4DC {2}",
                timeText,
                result.MiningCount,
                result.GoldEarned);

            offlineRewardModal.SetActive(true);
            offlineRewardModal.transform.SetAsLastSibling();
        }

        private void Start()
        {
            var overlayManager = LoadingOverlayManager.Instance;
            if (overlayManager != null)
            {
                overlayManager.Clear();
                overlayOwned = false;
            }

            SetupGameExitModalButtons();
            SetupOfflineModeDetailModalButtons();
            SetupOfflineRewardModalButtons();

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
            ShowLoadingOverlay("게임 서버 연결 중...");
            if (gameUIRoot != null)
                gameUIRoot.SetActive(false);

            // NetworkManager와 MessageHandler 초기화
            networkManager = NetworkManager.Instance;
            messageHandler = MessageHandler.Instance;

            // 서버 연결 시작
            _ = ConnectToServerAsync();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                RequestExitWithOfflineMode();
            }
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
                messageHandler.OnUserDataSnapshot += HandleUserDataSnapshot;
                messageHandler.OnErrorNotification += HandleErrorNotification;
                messageHandler.OnOfflineRewardResult += HandleOfflineRewardResult;
                messageHandler.OnOfflineModeStartResult += HandleOfflineModeStartResult;
            }

            if (networkManager != null)
            {
                networkManager.OnDisconnected += HandleDisconnected;
                networkManager.OnReconnecting += HandleReconnecting;
            }
        }

        private void OnDisable()
        {
            CancelReconnectOverlay();
            if (messageHandler != null)
            {
                messageHandler.OnHandshakeResult -= HandleHandshakeResult;
                messageHandler.OnUserDataSnapshot -= HandleUserDataSnapshot;
                messageHandler.OnErrorNotification -= HandleErrorNotification;
                messageHandler.OnOfflineRewardResult -= HandleOfflineRewardResult;
                messageHandler.OnOfflineModeStartResult -= HandleOfflineModeStartResult;
            }

            if (networkManager != null)
            {
                networkManager.OnDisconnected -= HandleDisconnected;
                networkManager.OnReconnecting -= HandleReconnecting;
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
                ShowLoadingOverlay("핸드셰이크 진행 중...");

                // 핸드셰이크 응답 대기 (타임아웃)
                float timeoutTime = Time.time + handshakeTimeoutSeconds;
                while (!isHandshakeCompleted && !isHandshakeFailed && Time.time < timeoutTime)
                {
                    await Task.Delay(100);
                }

                if (isHandshakeCompleted)
                {
                    Debug.Log("핸드셰이크 성공! 초기 데이터 대기 중...");
                    if (!isSnapshotReceived && !isGameReady)
                    {
                        ShowLoadingOverlay("게임 데이터를 불러오는 중...");
                    }
                    if (!isGameReady)
                    {
                        await WaitForSnapshotAsync();
                    }
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
            CancelReconnectOverlay();
            if (result.Success)
            {
                Debug.Log($"핸드셰이크 성공: {result.Message}");
                isHandshakeCompleted = true;
                isHandshakeFailed = false;
                if (result.Snapshot != null)
                {
                    isSnapshotReceived = true;
                }
                // 최초 게임 진입 시 슬롯/강화 상태를 조회해 UI가 바로 그릴 수 있도록 요청
                if (messageHandler != null)
                {
                    messageHandler.RequestAllSlots();
                }

                if (isGameReady)
                {
                    HideLoadingOverlay();
                    LoadingOverlayManager.Instance.Clear();
                    overlayOwned = false;
                    if (gameUIRoot != null)
                    {
                        gameUIRoot.SetActive(true);
                    }
                    return;
                }

                ShowLoadingOverlay("게임 데이터를 불러오는 중...");
                TryFinalizeGameReady();
            }
            else
            {
                Debug.LogError($"핸드셰이크 실패: {result.Message}");
                isHandshakeFailed = true;
            }
        }

        private void HandleUserDataSnapshot(UserDataSnapshot snapshot)
        {
            isSnapshotReceived = true;
            TryFinalizeGameReady();
        }

        private async Task WaitForSnapshotAsync()
        {
            if (isSnapshotReceived)
            {
                TryFinalizeGameReady();
                return;
            }

            float timeoutTime = Time.time + snapshotTimeoutSeconds;
            while (!isSnapshotReceived && !isHandshakeFailed && Time.time < timeoutTime)
            {
                await Task.Delay(100);
            }

            if (isSnapshotReceived)
            {
                TryFinalizeGameReady();
                return;
            }

            if (isHandshakeFailed)
            {
                FailAndReturnToTitle("인증에 실패했습니다. 다시 로그인해주세요.", clearSession: true);
                return;
            }

            Debug.LogError("유저 데이터 스냅샷 타임아웃");
            FailAndReturnToTitle("초기 데이터 수신 시간이 초과되었습니다. 다시 시도해주세요.");
        }

        private void TryFinalizeGameReady()
        {
            if (isGameReady) return;
            if (!isHandshakeCompleted || !isSnapshotReceived) return;

            isGameReady = true;
            HideLoadingOverlay();
            LoadingOverlayManager.Instance.Clear();
            overlayOwned = false;
            OnGameReady();
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
            CancelReconnectOverlay();
            if (suppressDisconnectNotice)
            {
                suppressDisconnectNotice = false;
                return;
            }
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
        /// 재연결 시작 처리
        /// </summary>
        private void HandleReconnecting(string reason)
        {
            StartReconnectOverlayDelay();
        }

        /// <summary>
        /// 게임 준비 완료 (핸드셰이크 성공)
        /// </summary>
        private void OnGameReady()
        {
            // 로딩 패널 숨기기
            SetLocalLoadingVisible(false);

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
            CancelReconnectOverlay();
            Debug.LogError($"연결 오류: {message}");

            TitleController.SetReconnectNotice(message);
            HideLoadingOverlay();

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
