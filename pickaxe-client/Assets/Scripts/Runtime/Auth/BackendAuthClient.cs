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
        private sealed class LoginRequest
        {
            public string provider;
            public string token;
            public string device_id;
            public string email;
        }

        [Serializable]
        private sealed class VerifyRequest
        {
            public string jwt;
            public string refresh_token;
            public string device_id;
        }

        [Serializable]
        private sealed class LoginResponseDto
        {
            public bool success;
            public string error;
            public string jwt;
            public string refresh_token;
            public string user_id;
            public string nickname;
            public string email;
            public string provider;
        }

        [Serializable]
        private sealed class VerifyResponseDto
        {
            public bool valid;
            public string error;
            public string jwt;
            public string refresh_token;
            public long refresh_expires_at;
            public string user_id;
            public string external_id;
            public string provider;
            public string email;
            public string nickname;
            public string device_id;
            public long expires_at;
        }

        public BackendAuthClient(string baseUri, int timeoutSeconds = 10)
        {
            this.baseUri = baseUri.TrimEnd('/');
            this.timeoutSeconds = timeoutSeconds;
        }

        public async Task<AuthResult> LoginAsync(string provider, string token, string deviceId, string email = null)
        {
            var payload = new LoginRequest { provider = provider, token = token, device_id = deviceId, email = email };
            return await PostLoginAsync("/auth/login", payload);
        }

        public async Task<AuthResult> VerifyAsync(string refreshToken, string deviceId = null, string jwt = null)
        {
            var payload = new VerifyRequest
            {
                jwt = jwt,
                refresh_token = refreshToken,
                device_id = deviceId
            };
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

                    return AuthResult.Ok(dto.user_id, dto.nickname, dto.email, dto.provider, dto.jwt, dto.refresh_token, null);
                }
                catch (Exception ex)
                {
                    return AuthResult.Fail($"Parse error: {ex.Message}");
                }
            }
        }

        private async Task<AuthResult> PostVerifyAsync(string path, VerifyRequest payload)
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

                    var jwtToUse = string.IsNullOrEmpty(dto.jwt) ? payload.jwt : dto.jwt;
                    var refreshToUse = string.IsNullOrEmpty(dto.refresh_token) ? payload.refresh_token : dto.refresh_token;
                    return AuthResult.Ok(dto.user_id, dto.nickname, dto.email, dto.provider, jwtToUse, refreshToUse, null);
                }
                catch (Exception ex)
                {
                    return AuthResult.Fail($"Parse error: {ex.Message}");
                }
            }
        }

        [Serializable]
        private sealed class NicknameRequest
        {
            public string jwt;
            public string nickname;
        }

        [Serializable]
        private sealed class NicknameResponseDto
        {
            public bool success;
            public string error;
            public string nickname;
        }

        public async Task<AuthResult> SetNicknameAsync(string jwt, string nickname)
        {
            var payload = new NicknameRequest { jwt = jwt, nickname = nickname };
            var url = $"{baseUri}/auth/nickname";
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
                    var dto = JsonUtility.FromJson<NicknameResponseDto>(request.downloadHandler.text);
                    if (dto == null)
                    {
                        return AuthResult.Fail("Empty nickname response");
                    }
                    if (!dto.success)
                    {
                        return AuthResult.Fail(string.IsNullOrEmpty(dto.error) ? "NICKNAME_FAILED" : dto.error);
                    }
                    return AuthResult.Ok(null, dto.nickname, null, null, jwt, null, null);
                }
                catch (Exception ex)
                {
                    return AuthResult.Fail($"Parse error: {ex.Message}");
                }
            }
        }
    }
}
