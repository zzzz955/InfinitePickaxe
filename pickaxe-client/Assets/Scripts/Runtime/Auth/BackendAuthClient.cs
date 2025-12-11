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
            public string google_token;
            public string device_id;
        }

        [Serializable]
        private sealed class RefreshRequest
        {
            public string jwt;
        }

        [Serializable]
        private sealed class LoginResponseDto
        {
            public bool success;
            public string error;
            public string jwt;
            public string refresh_token;
            public string user_id;
            public bool is_new_user;
            public long server_time;
        }

        private sealed class VerifyResponseDto
        {
            public bool valid;
            public string error;
            public string jwt;
            public string refresh_token;
            public long refresh_expires_at;
            public string user_id;
            public string google_id;
            public string device_id;
            public long expires_at;
        }

        public BackendAuthClient(string baseUri, int timeoutSeconds = 10)
        {
            this.baseUri = baseUri.TrimEnd('/');
            this.timeoutSeconds = timeoutSeconds;
        }

        public async Task<AuthResult> LoginWithGoogleAsync(string googleIdToken, string deviceId)
        {
            var payload = new GoogleLoginRequest { google_token = googleIdToken, device_id = deviceId };
            return await PostLoginAsync("/auth/login", payload);
        }

        public async Task<AuthResult> VerifyAsync(string jwt)
        {
            var payload = new RefreshRequest { jwt = jwt };
            return await PostVerifyAsync("/auth/verify", payload);
        }

        private async Task<AuthResult> PostLoginAsync(string path, object payload)
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
                    var dto = JsonUtility.FromJson<LoginResponseDto>(request.downloadHandler.text);
                    if (dto == null)
                    {
                        return AuthResult.Fail("Empty auth response");
                    }

                    if (!dto.success)
                    {
                        return AuthResult.Fail(string.IsNullOrEmpty(dto.error) ? "Auth failed" : dto.error);
                    }

                    return AuthResult.Ok(dto.user_id, null, dto.jwt, dto.refresh_token, null);
                }
                catch (Exception ex)
                {
                    return AuthResult.Fail($"Parse error: {ex.Message}");
                }
            }
        }

        private async Task<AuthResult> PostVerifyAsync(string path, object payload)
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
                    var dto = JsonUtility.FromJson<VerifyResponseDto>(request.downloadHandler.text);
                    if (dto == null)
                    {
                        return AuthResult.Fail("Empty verify response");
                    }

                    if (!dto.valid)
                    {
                        return AuthResult.Fail(string.IsNullOrEmpty(dto.error) ? "VERIFY_FAILED" : dto.error);
                    }

                    var jwt = string.IsNullOrEmpty(dto.jwt) ? ((RefreshRequest)payload).jwt : dto.jwt;
                    return AuthResult.Ok(dto.user_id, null, jwt, dto.refresh_token, null);
                }
                catch (Exception ex)
                {
                    return AuthResult.Fail($"Parse error: {ex.Message}");
                }
            }
        }
    }
}
