using InfinitePickaxe.Client.Config;
using InfinitePickaxe.Client.Core;
using InfinitePickaxe.Client.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InfinitePickaxe.Client.Bootstrap
{
    public sealed class ClientBootstrap : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private ClientConfigAsset configAsset;
        [SerializeField] private string jsonResourcePath = "config";
        [SerializeField] private bool initializeOnAwake = true;
        [SerializeField] private bool loadTitleAfterInit = true;
        [SerializeField] private string titleSceneName = "Title";

        private static ClientBootstrap instance;
        private bool initializedHere;
        private bool bootSequenceStarted;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            if (initializeOnAwake)
            {
                InitializeRuntime();
            }

            if (loadTitleAfterInit && !bootSequenceStarted)
            {
                bootSequenceStarted = true;
                StartCoroutine(BootSequence());
            }
        }

        public void InitializeRuntime()
        {
            if (ClientRuntime.IsInitialized)
            {
                return;
            }

            var config = ClientConfigLoader.Load(configAsset, jsonResourcePath);
            var networkSettings = NetworkSettings.FromConfig(config);
            var transport = new LengthPrefixedTcpTransport();
            var networkClient = new NetworkClient(transport, networkSettings);

            ClientRuntime.Initialize(config, registry =>
            {
                registry.RegisterSingleton(networkSettings);
                registry.RegisterSingleton<INetworkTransport>(transport);
                registry.RegisterSingleton(networkClient);
                registry.RegisterSingleton(new GameSessionState());
            });

            initializedHere = true;
        }

        private void OnApplicationQuit()
        {
            if (initializedHere)
            {
                ClientRuntime.Dispose();
            }
        }

        private System.Collections.IEnumerator BootSequence()
        {
            // Wait one frame to ensure Awake lifecycle of other bootstrap components runs.
            yield return null;

            // Place any required pre-load tasks here (e.g., warmup caches, load static data).
            yield return LoadEssentialData();

            if (!string.IsNullOrWhiteSpace(titleSceneName))
            {
                SceneManager.LoadScene(titleSceneName);
            }
            else
            {
                Debug.LogError("ClientBootstrap: titleSceneName is not set, cannot load title scene.");
            }
        }

        private System.Collections.IEnumerator LoadEssentialData()
        {
            // Stub for future data preloading. Extend with actual loading coroutines when ready.
            yield break;
        }
    }
}
