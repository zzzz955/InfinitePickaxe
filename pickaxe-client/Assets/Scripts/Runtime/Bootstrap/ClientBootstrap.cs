using InfinitePickaxe.Client.Config;
using InfinitePickaxe.Client.Core;
using InfinitePickaxe.Client.Net;
using UnityEngine;

namespace InfinitePickaxe.Client.Bootstrap
{
    public sealed class ClientBootstrap : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private ClientConfigAsset configAsset;
        [SerializeField] private string jsonResourcePath = "config";
        [SerializeField] private bool initializeOnAwake = true;

        private static ClientBootstrap instance;
        private bool initializedHere;

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
    }
}
