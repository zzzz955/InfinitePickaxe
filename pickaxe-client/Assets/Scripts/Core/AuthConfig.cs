using System;
using UnityEngine;

namespace Pickaxe.Client.Core
{
    [Serializable]
    public class AuthConfig
    {
        public string webClientId;
        public bool requestIdToken = true;
        public bool forceTokenRefresh = true;

        private const string ResourcePath = "Config/auth_config";

        public static AuthConfig Load()
        {
            var text = Resources.Load<TextAsset>(ResourcePath);
            if (text == null)
            {
                Debug.LogError($"AuthConfig not found at Resources/{ResourcePath}.json");
                return null;
            }
            return JsonUtility.FromJson<AuthConfig>(text.text);
        }
    }
}
