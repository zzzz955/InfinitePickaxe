using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace InfinitePickaxe.Client.Auth
{
    public sealed class BackendAuthClient
    {
        private readonly string baseUri;
        private readonly int timeoutSeconds;

        [Serializable]
        private sealed class GoogleLoginRequest
        {
            public string id_token;
        }

        [Serializable]
        private sealed class RefreshRequest
        {
            public string refresh_token;
        }

        [Serializable]
        private sealed class AuthResponseDto
        {
            public bool ok;
            public string error;
            public string access_token;
            public string refresh_token;
            public string user_id;
            public string display_name;
        }

        public BackendAuthClient(string baseUri, int timeoutSeconds = 10)
        {
            this.baseUri = baseUri.TrimEnd('/');
            this.timeoutSeconds = timeoutSeconds;
        }

        public async Task<AuthResult> LoginWithGoogleAsync(string googleIdToken)
        {
            var payload = new GoogleLoginRequest { id_token = googleIdToken };
            return await PostAuthAsync("/auth/google", payload);
        }

        public async Task<AuthResult> RefreshAsync(string refreshToken)
        {
            var payload = new RefreshRequest { refresh_token = refreshToken };
            return await PostAuthAsync("/auth/refresh", payload);
        }

        private async Task<AuthResult> PostAuthAsync(string path, object payload)
        {
            var url = $"{baseUri}{path}";
            var json = JsonUtility.ToJson(payload);
            using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                var bodyRaw = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = timeoutSeconds;

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

#if UNITY_2020_1_OR_NEWER
                var isError = request.result != UnityWebRequest.Result.Success;
#else
                var isError = request.isNetworkError || request.isHttpError;
#endif

                if (isError)
                {
                    return AuthResult.Fail($"HTTP {request.responseCode}: {request.error}");
                }

                try
                {
                    var dto = JsonUtility.FromJson<AuthResponseDto>(request.downloadHandler.text);
                    if (dto == null)
                    {
                        return AuthResult.Fail("Empty auth response");
                    }

                    if (!dto.ok)
                    {
                        return AuthResult.Fail(string.IsNullOrEmpty(dto.error) ? "Auth failed" : dto.error);
                    }

                    return AuthResult.Ok(dto.user_id, dto.display_name, dto.access_token, dto.refresh_token, null);
                }
                catch (Exception ex)
                {
                    return AuthResult.Fail($"Parse error: {ex.Message}");
                }
            }
        }
    }
}
