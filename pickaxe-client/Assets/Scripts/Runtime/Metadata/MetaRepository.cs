using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using InfinitePickaxe.Client.Metadata.MiniJson;

namespace InfinitePickaxe.Client.Metadata
{
    /// <summary>
    /// 메타데이터(json)를 디스크에서 읽어 인메모리에 보관하는 저장소.
    /// </summary>
    public static class MetaRepository
    {
        private static Dictionary<string, object> data;
        private static string rawJson;
        private static string hash;

        public static bool Loaded => data != null;
        public static IReadOnlyDictionary<string, object> Data => data;
        public static string RawJson => rawJson;
        public static string Hash => hash;

        /// <summary>
        /// 메타 파일을 읽고 파싱해 인메모리에 적재한다.
        /// </summary>
        /// <param name="path">meta_bundle.json 경로</param>
        /// <param name="expectedHash">옵션: 캐시에 저장된 해시(검증용)</param>
        public static bool LoadFromFile(string path, string expectedHash = "")
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"MetaRepository: file not found: {path}");
                data = null;
                rawJson = null;
                hash = null;
                return false;
            }

            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                var parsed = Json.Deserialize(json) as Dictionary<string, object>;
                if (parsed == null)
                {
                    Debug.LogError("MetaRepository: failed to parse meta json (unexpected shape).");
                    data = null;
                    rawJson = null;
                    hash = null;
                    return false;
                }

                var computedHash = ComputeSha256Hex(Encoding.UTF8.GetBytes(json));
                if (!string.IsNullOrEmpty(expectedHash) && !string.Equals(expectedHash, computedHash, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning($"MetaRepository: hash mismatch. expected={expectedHash}, file={computedHash}");
                }

                data = parsed;
                rawJson = json;
                hash = computedHash;
                Debug.Log($"MetaRepository: loaded meta. size={json.Length} hash={computedHash}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"MetaRepository: load failed: {ex.Message}");
                data = null;
                rawJson = null;
                hash = null;
                return false;
            }
        }

        private static string ComputeSha256Hex(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                var hashBytes = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hashBytes.Length * 2);
                foreach (var b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }
}
