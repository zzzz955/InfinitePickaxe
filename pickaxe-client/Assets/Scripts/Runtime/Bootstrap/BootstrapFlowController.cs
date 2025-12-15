using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using InfinitePickaxe.Client.Metadata;
using InfinitePickaxe.Client.Config;

namespace InfinitePickaxe.Client.Bootstrap
{
    /// <summary>
    /// 인증 서버 /bootstrap 호출, 프로토콜/메타 체크, Title 전환 관리
    /// </summary>
    public class BootstrapFlowController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private ClientConfigAsset configAsset;
        [SerializeField] private string jsonResourcePath = "config";
        [SerializeField] private string protocolVersion = "1.0";
        [SerializeField] private string bootstrapPath = "/bootstrap";
        [SerializeField] private string nextScene = "Title";
        [SerializeField] private bool autoStart = true;
        [SerializeField] private int timeoutSeconds = 8;

        [Header("UI References (optional, auto-find by name)")]
        [SerializeField] private GameObject overlayRoot;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject modalPanel;
        [SerializeField] private TextMeshProUGUI modalTitle;
        [SerializeField] private TextMeshProUGUI modalMessage;
        [SerializeField] private Button primaryButton;
        [SerializeField] private Button secondaryButton;

        private const string MetaHashKey = "meta_hash";
        private const string MetaFileName = "meta_bundle.json";
        private bool running;

        private void Awake()
        {
            AutoBind();
        }

        private void Start()
        {
            if (autoStart && !running)
            {
                StartCoroutine(RunBootstrap());
            }
        }

        private void AutoBind()
        {
            if (overlayRoot == null) overlayRoot = GameObject.Find("LoadingOverlay");
            if (statusText == null)
            {
                var t = GameObject.Find("StatusText");
                if (t != null) statusText = t.GetComponent<TextMeshProUGUI>();
            }

            if (modalPanel == null) modalPanel = GameObject.Find("ModalPanel");
            if (modalTitle == null)
            {
                var t = GameObject.Find("Title");
                if (t != null) modalTitle = t.GetComponent<TextMeshProUGUI>();
            }
            if (modalMessage == null)
            {
                var t = GameObject.Find("Message");
                if (t != null) modalMessage = t.GetComponent<TextMeshProUGUI>();
            }
            if (primaryButton == null)
            {
                var b = GameObject.Find("PrimaryButton");
                if (b != null) primaryButton = b.GetComponent<Button>();
            }
            if (secondaryButton == null)
            {
                var b = GameObject.Find("SecondaryButton");
                if (b != null) secondaryButton = b.GetComponent<Button>();
            }
        }

        private IEnumerator RunBootstrap()
        {
            running = true;
            ShowOverlay(true, "서버 연결 중...");

            var envConfig = LoadConfig();
            if (envConfig == null)
            {
                ShowError("설정 로드 실패", "config를 불러오지 못했습니다.", canRetry: true, storeUrl: null);
                yield break;
            }

            var scheme = envConfig.useTls ? "https" : "http";
            var baseUrl = $"{scheme}://{envConfig.host}:{envConfig.authPort}";
            var url = $"{baseUrl}{bootstrapPath}";

            var cachedHash = GetCachedMetaHash();
            var payload = new BootstrapRequestDto
            {
                app_version = Application.version,
                protocol_version = protocolVersion,
                build_number = Application.version,
                device_id = SystemInfo.deviceUniqueIdentifier,
                locale = Application.systemLanguage.ToString(),
                cached_meta_hash = cachedHash
            };

            var json = JsonUtility.ToJson(payload);
            using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                var bodyRaw = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = timeoutSeconds;

                var op = request.SendWebRequest();
                while (!op.isDone)
                {
                    yield return null;
                }

#if UNITY_2020_1_OR_NEWER
                var isError = request.result != UnityWebRequest.Result.Success;
#else
                var isError = request.isNetworkError || request.isHttpError;
#endif
                if (isError)
                {
                    Debug.LogError($"Bootstrap HTTP error {request.responseCode}: {request.error}");
                    ShowError("네트워크 오류", $"HTTP {request.responseCode}: {request.error}", canRetry: true, storeUrl: null);
                    yield break;
                }

                BootstrapResponseDto resp = null;
                try
                {
                    var raw = request.downloadHandler.text;
                    Debug.Log($"Bootstrap raw response: {raw}");
                    resp = JsonUtility.FromJson<BootstrapResponseDto>(raw);
                }
                catch (Exception ex)
                {
                    ShowError("파싱 오류", ex.Message, canRetry: true, storeUrl: null);
                    yield break;
                }

                if (resp == null)
                {
                    ShowError("응답 오류", "빈 응답입니다.", canRetry: true, storeUrl: null);
                    yield break;
                }

                HandleResponse(resp);
            }
        }

        private void HandleResponse(BootstrapResponseDto resp)
        {
            switch (resp.action)
            {
                case "PROCEED":
                {
                    if (resp.meta != null)
                    {
                        var hasPayload = !string.IsNullOrEmpty(resp.meta.data) || !string.IsNullOrEmpty(resp.meta.download_url);
                        if (!hasPayload)
                        {
                            Debug.Log("Bootstrap: meta object present but no data/download_url, skipping apply.");
                        }
                        else if (!ApplyMeta(resp.meta))
                        {
                            ShowError("메타 검증 실패", "메타데이터 해시가 일치하지 않습니다.", canRetry: true, storeUrl: null);
                            return;
                        }
                    }
                    StartCoroutine(LoadMetaAndProceed());
                    break;
                }

                case "UPDATE_REQUIRED":
                    ShowError("업데이트 필요", string.IsNullOrEmpty(resp.message) ? "최신 버전으로 업데이트가 필요합니다." : resp.message, canRetry: false, storeUrl: resp.store_url);
                    break;

                case "MAINTENANCE":
                    ShowError("점검 중", string.IsNullOrEmpty(resp.message) ? "점검이 진행 중입니다." : resp.message, canRetry: true, storeUrl: null);
                    break;

                case "BLOCKED":
                    ShowError("접속 차단", string.IsNullOrEmpty(resp.message) ? "접속이 차단되었습니다." : resp.message, canRetry: false, storeUrl: null);
                    break;

                default:
                    ShowError("알 수 없는 상태", resp.message ?? "처리할 수 없는 응답입니다.", canRetry: true, storeUrl: null);
                    break;
            }
        }

        private bool ApplyMeta(MetaDto meta)
        {
            byte[] dataBytes = null;
            if (!string.IsNullOrEmpty(meta.data))
            {
                try
                {
                    dataBytes = Convert.FromBase64String(meta.data);
                }
                catch
                {
                    Debug.LogError("ApplyMeta: base64 decode failed");
                    return false;
                }
            }
            else
            {
                // download_url 미지원 (현재 스냅샷 응답만 처리)
                Debug.LogError("ApplyMeta: no data or download_url provided");
                return false;
            }

            var computed = ComputeSha256Hex(dataBytes);
            var expected = string.IsNullOrEmpty(meta.hash) ? computed : meta.hash;
            if (string.IsNullOrEmpty(meta.hash))
            {
                Debug.LogWarning("ApplyMeta: meta.hash empty, using computed hash.");
            }
            if (!string.Equals(computed, expected, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError($"Meta hash mismatch. expected={expected}, computed={computed}");
                return false;
            }

            try
            {
                var dir = Path.Combine(Application.persistentDataPath, "meta");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "meta_bundle.json");
                File.WriteAllBytes(path, dataBytes);
                var storedHash = string.IsNullOrEmpty(meta.hash) ? computed : meta.hash;
                PlayerPrefs.SetString(MetaHashKey, storedHash);
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"메타 저장 실패: {ex.Message}");
                return false;
            }
        }

        private void ShowOverlay(bool show, string message = null)
        {
            if (overlayRoot != null) overlayRoot.SetActive(show);
            if (statusText != null && message != null) statusText.text = message;
        }

        private IEnumerator LoadMetaAndProceed()
        {
            ShowOverlay(true, "메타 데이터 로드 중...");

            var path = Path.Combine(Application.persistentDataPath, "meta", MetaFileName);
            var hash = PlayerPrefs.GetString(MetaHashKey, string.Empty);

            if (!MetaRepository.LoadFromFile(path, hash))
            {
                // 메타 로드 실패 시 전체 부트스트랩을 다시 시도하여 서버로부터 재다운로드
                ShowError("메타 로드 실패", "메타데이터를 불러올 수 없습니다.", canRetry: true, storeUrl: null, onRetry: () => StartCoroutine(RunBootstrap()));
                yield break;
            }

            ShowOverlay(false);
            LoadNextScene();
        }

        private void ShowError(string title, string message, bool canRetry, string storeUrl, Action onRetry = null)
        {
            ShowOverlay(false);
            if (modalPanel != null) modalPanel.SetActive(true);
            if (modalTitle != null) modalTitle.text = title;
            if (modalMessage != null) modalMessage.text = message;

            if (primaryButton != null)
            {
                primaryButton.onClick.RemoveAllListeners();
                if (!string.IsNullOrEmpty(storeUrl))
                {
                    primaryButton.onClick.AddListener(() =>
                    {
                        Application.OpenURL(storeUrl);
                        Application.Quit();
                    });
                }
                else if (canRetry)
                {
                    primaryButton.onClick.AddListener(() =>
                    {
                        if (modalPanel != null) modalPanel.SetActive(false);
                        if (onRetry != null)
                        {
                            onRetry();
                        }
                        else
                        {
                            StartCoroutine(RunBootstrap());
                        }
                    });
                }
                else
                {
                    primaryButton.onClick.AddListener(() => Application.Quit());
                }
            }

            if (secondaryButton != null)
            {
                secondaryButton.onClick.RemoveAllListeners();
                secondaryButton.onClick.AddListener(() => Application.Quit());
            }
        }

        private void LoadNextScene()
        {
            if (string.IsNullOrEmpty(nextScene))
            {
                Debug.LogWarning("BootstrapFlow: nextScene is empty.");
                return;
            }
            SceneManager.LoadScene(nextScene);
        }

        private EnvironmentConfig LoadConfig()
        {
            var data = ClientConfigLoader.Load(configAsset, jsonResourcePath);
            return data?.GetActiveEnvironment();
        }

        private string GetCachedMetaHash()
        {
            var cached = PlayerPrefs.GetString(MetaHashKey, string.Empty);
            var path = Path.Combine(Application.persistentDataPath, "meta", MetaFileName);
            if (!File.Exists(path))
            {
                if (!string.IsNullOrEmpty(cached))
                {
                    Debug.LogWarning("Cached meta hash exists but file is missing; forcing re-download.");
                }
                return string.Empty;
            }

            try
            {
                var bytes = File.ReadAllBytes(path);
                var fileHash = ComputeSha256Hex(bytes);
                if (!string.Equals(cached, fileHash, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(cached))
                {
                    Debug.LogWarning($"Cached meta hash mismatch with file. cached={cached}, file={fileHash}. Using file hash.");
                }
                if (!string.Equals(cached, fileHash, StringComparison.OrdinalIgnoreCase))
                {
                    PlayerPrefs.SetString(MetaHashKey, fileHash);
                    PlayerPrefs.Save();
                }
                return fileHash;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to read meta bundle for hash: {ex.Message}");
                return string.Empty;
            }
        }

        private static string ComputeSha256Hex(byte[] data)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(data);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }

        [Serializable]
        private class BootstrapRequestDto
        {
            public string app_version;
            public string protocol_version;
            public string build_number;
            public string device_id;
            public string locale;
            public string cached_meta_hash;
        }

        [Serializable]
        private class BootstrapResponseDto
        {
            public string action;
            public string message;
            public string store_url;
            public int retry_after_seconds;
            public MetaDto meta;
        }

        [Serializable]
        private class MetaDto
        {
            public string hash;
            public long size_bytes;
            public string download_url;
            public string data;
        }
    }
}
