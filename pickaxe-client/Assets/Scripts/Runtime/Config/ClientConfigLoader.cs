using System;
using UnityEngine;

namespace InfinitePickaxe.Client.Config
{
    public static class ClientConfigLoader
    {
        private const string DefaultJsonResourcePath = "config";
        private const int MinMessageBytes = 4 * 1024;
        private const int MaxMessageBytes = 4 * 1024 * 1024;

        public static ClientConfigData Load(ClientConfigAsset asset = null, string jsonResourcePath = DefaultJsonResourcePath)
        {
            if (asset != null && asset.Data != null)
            {
                return Sanitize(Clone(asset.Data));
            }

            var textAsset = Resources.Load<TextAsset>(jsonResourcePath);
            if (textAsset == null)
            {
                Debug.LogWarning($"Client config json not found at Resources/{jsonResourcePath}. Using defaults.");
                return Sanitize(ClientConfigData.Default());
            }

            try
            {
                var data = JsonUtility.FromJson<ClientConfigData>(textAsset.text);
                if (data == null)
                {
                    Debug.LogWarning("Client config json deserialized to null. Using defaults.");
                    return Sanitize(ClientConfigData.Default());
                }

                return Sanitize(data);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to parse client config json: {ex}");
                return Sanitize(ClientConfigData.Default());
            }
        }

        private static ClientConfigData Clone(ClientConfigData source)
        {
            if (source == null)
            {
                return null;
            }

            var json = JsonUtility.ToJson(source);
            return JsonUtility.FromJson<ClientConfigData>(json);
        }

        private static ClientConfigData Sanitize(ClientConfigData data)
        {
            if (data == null)
            {
                data = ClientConfigData.Default();
            }

            if (data.environments == null || data.environments.Length == 0)
            {
                data.environments = ClientConfigData.Default().environments;
            }

            data.connectTimeoutSeconds = Mathf.Max(0.5f, data.connectTimeoutSeconds);
            data.receiveTimeoutSeconds = Mathf.Max(0.5f, data.receiveTimeoutSeconds);
            data.sendTimeoutSeconds = Mathf.Max(0.5f, data.sendTimeoutSeconds);
            data.heartbeatIntervalSeconds = Mathf.Max(5f, data.heartbeatIntervalSeconds);
            data.maxMessageBytes = Mathf.Clamp(data.maxMessageBytes, MinMessageBytes, MaxMessageBytes);

            return data;
        }
    }
}
