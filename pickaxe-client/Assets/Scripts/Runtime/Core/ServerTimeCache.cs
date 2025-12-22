using System;

namespace InfinitePickaxe.Client.Core
{
    public sealed class ServerTimeCache
    {
        private static readonly Lazy<ServerTimeCache> Lazy = new Lazy<ServerTimeCache>(() => new ServerTimeCache());
        public static ServerTimeCache Instance => Lazy.Value;

        private bool hasServerTime;
        private long offsetMs;

        private ServerTimeCache() { }

        public bool HasServerTime => hasServerTime;

        public void Update(ulong serverTimeMs)
        {
            long localMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            offsetMs = unchecked((long)serverTimeMs) - localMs;
            hasServerTime = true;
        }

        public long NowMs
        {
            get
            {
                long localMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                return hasServerTime ? localMs + offsetMs : localMs;
            }
        }

        public DateTimeOffset NowUtc => DateTimeOffset.FromUnixTimeMilliseconds(NowMs);
    }
}
