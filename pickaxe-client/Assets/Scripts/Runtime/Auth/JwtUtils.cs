using System;
using System.Text;

namespace InfinitePickaxe.Client.Auth
{
    public static class JwtUtils
    {
        public static long GetUnixTimeSeconds() =>
            DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public static bool TryGetExpiry(string jwt, out long expSeconds)
        {
            expSeconds = 0;
            if (string.IsNullOrWhiteSpace(jwt)) return false;

            var parts = jwt.Split('.');
            if (parts.Length < 2) return false;

            try
            {
                var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
                const string key = "\"exp\":";
                var idx = payloadJson.IndexOf(key, StringComparison.Ordinal);
                if (idx < 0) return false;
                idx += key.Length;
                while (idx < payloadJson.Length && char.IsWhiteSpace(payloadJson[idx])) idx++;
                var start = idx;
                while (idx < payloadJson.Length && char.IsDigit(payloadJson[idx])) idx++;
                if (idx <= start) return false;
                var number = payloadJson.Substring(start, idx - start);
                if (long.TryParse(number, out var val))
                {
                    expSeconds = val;
                    return true;
                }
            }
            catch
            {
                return false;
            }
            return false;
        }

        private static byte[] Base64UrlDecode(string input)
        {
            input = input.Replace('-', '+').Replace('_', '/');
            switch (input.Length % 4)
            {
                case 2: input += "=="; break;
                case 3: input += "="; break;
            }
            return Convert.FromBase64String(input);
        }
    }
}
