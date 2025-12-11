using System;
using System.Text;
using UnityEngine;

namespace InfinitePickaxe.Client.Auth
{
    /// <summary>
    /// Minimal Android Keystore-backed encrypt/decrypt helper (AES/GCM).
    /// Encrypted payload is stored Base64 (IV + ciphertext) in PlayerPrefs or any string field.
    /// </summary>
    public static class AndroidSecureStorage
    {
        private const string KeystoreProvider = "AndroidKeyStore";
        private const string CipherTransformation = "AES/GCM/NoPadding";
        private const int KeySizeBits = 256;
        private const int GcmTagLengthBits = 128;
        private const int PurposeEncryptDecrypt = 3; // ENCRYPT | DECRYPT

        public static string Encrypt(string alias, string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return string.Empty;
            }

            try
            {
                var secretKey = GetOrCreateKey(alias);
                var cipher = GetCipher();
                cipher.Call("init", 1, secretKey); // Cipher.ENCRYPT_MODE = 1

                var iv = cipher.Call<byte[]>("getIV");
                var plainBytes = Encoding.UTF8.GetBytes(plainText);
                var cipherBytes = cipher.Call<byte[]>("doFinal", plainBytes);

                var combined = new byte[iv.Length + cipherBytes.Length];
                Buffer.BlockCopy(iv, 0, combined, 0, iv.Length);
                Buffer.BlockCopy(cipherBytes, 0, combined, iv.Length, cipherBytes.Length);
                return Convert.ToBase64String(combined);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"AndroidSecureStorage.Encrypt failed: {ex}");
                return string.Empty;
            }
        }

        public static string Decrypt(string alias, string cipherBase64)
        {
            if (string.IsNullOrEmpty(cipherBase64))
            {
                return string.Empty;
            }

            try
            {
                var cipherData = Convert.FromBase64String(cipherBase64);
                if (cipherData.Length < 12)
                {
                    return string.Empty;
                }

                var ivLength = 12; // GCM recommended IV size
                var iv = new byte[ivLength];
                var encrypted = new byte[cipherData.Length - ivLength];
                Buffer.BlockCopy(cipherData, 0, iv, 0, ivLength);
                Buffer.BlockCopy(cipherData, ivLength, encrypted, 0, encrypted.Length);

                var secretKey = GetOrCreateKey(alias);
                var cipher = GetCipher();
                var gcmSpec = new AndroidJavaObject("javax.crypto.spec.GCMParameterSpec", GcmTagLengthBits, iv);
                cipher.Call("init", 2, secretKey, gcmSpec); // Cipher.DECRYPT_MODE = 2

                var plainBytes = cipher.Call<byte[]>("doFinal", encrypted);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"AndroidSecureStorage.Decrypt failed: {ex}");
                return string.Empty;
            }
        }

        private static AndroidJavaObject GetCipher()
        {
            var cipherClass = new AndroidJavaClass("javax.crypto.Cipher");
            return cipherClass.CallStatic<AndroidJavaObject>("getInstance", CipherTransformation);
        }

        private static AndroidJavaObject GetOrCreateKey(string alias)
        {
            var keyStore = new AndroidJavaClass("java.security.KeyStore").CallStatic<AndroidJavaObject>("getInstance", KeystoreProvider);
            keyStore.Call("load", null);
            if (!keyStore.Call<bool>("containsAlias", alias))
            {
                GenerateKey(alias);
            }
            return keyStore.Call<AndroidJavaObject>("getKey", alias, null);
        }

        private static void GenerateKey(string alias)
        {
            var keyGenClass = new AndroidJavaClass("javax.crypto.KeyGenerator");
            var keyGen = keyGenClass.CallStatic<AndroidJavaObject>("getInstance", "AES", KeystoreProvider);

            var builder = new AndroidJavaObject(
                "android.security.keystore.KeyGenParameterSpec$Builder",
                alias,
                PurposeEncryptDecrypt);

            builder.Call<AndroidJavaObject>("setKeySize", KeySizeBits);
            builder.Call<AndroidJavaObject>("setBlockModes", "GCM");
            builder.Call<AndroidJavaObject>("setEncryptionPaddings", "NoPadding");
            builder.Call<AndroidJavaObject>("setRandomizedEncryptionRequired", true);

            var spec = builder.Call<AndroidJavaObject>("build");
            keyGen.Call("init", spec);
            keyGen.Call<AndroidJavaObject>("generateKey");
        }
    }
}
