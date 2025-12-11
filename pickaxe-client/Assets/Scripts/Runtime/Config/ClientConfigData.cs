using System;
using UnityEngine;

namespace InfinitePickaxe.Client.Config
{
    [Serializable]
    public class EnvironmentConfig
    {
        public string name = "dev";
        public string host = "127.0.0.1";
        public int port = 10001; // game/TCP
        public int authPort = 10000; // REST auth server
        public bool useTls;
    }

    [Serializable]
    public class ClientConfigData
    {
        public string environment = "dev";
        public EnvironmentConfig[] environments = Array.Empty<EnvironmentConfig>();
        public float connectTimeoutSeconds = 5f;
        public float receiveTimeoutSeconds = 5f;
        public float sendTimeoutSeconds = 5f;
        public float heartbeatIntervalSeconds = 30f;
        public int maxMessageBytes = 1024 * 1024;

        public EnvironmentConfig GetActiveEnvironment()
        {
            if (environments == null || environments.Length == 0)
            {
                return new EnvironmentConfig();
            }

            foreach (var env in environments)
            {
                if (string.Equals(env?.name, environment, StringComparison.OrdinalIgnoreCase))
                {
                    return env;
                }
            }

            return environments[0];
        }

        public static ClientConfigData Default()
        {
            return new ClientConfigData
            {
                environment = "dev",
                environments = new[]
                {
                    new EnvironmentConfig
                    {
                        name = "dev",
                        host = "127.0.0.1",
                        port = 10001,
                        authPort = 10000,
                        useTls = false
                    },
                    new EnvironmentConfig
                    {
                        name = "stage",
                        host = "stage.example.com",
                        port = 10001,
                        authPort = 10000,
                        useTls = true
                    },
                    new EnvironmentConfig
                    {
                        name = "prod",
                        host = "prod.example.com",
                        port = 10001,
                        authPort = 10000,
                        useTls = true
                    }
                },
                connectTimeoutSeconds = 5f,
                receiveTimeoutSeconds = 5f,
                sendTimeoutSeconds = 5f,
                heartbeatIntervalSeconds = 30f,
                maxMessageBytes = 1024 * 1024
            };
        }
    }

    [CreateAssetMenu(fileName = "ClientConfigAsset", menuName = "InfinitePickaxe/Client Config")]
    public sealed class ClientConfigAsset : ScriptableObject
    {
        [SerializeField] private ClientConfigData data = ClientConfigData.Default();

        public ClientConfigData Data => data;
    }
}
