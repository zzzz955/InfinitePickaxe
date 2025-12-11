namespace InfinitePickaxe.Client.Net
{
    public readonly struct NetworkEndpoint
    {
        public string Host { get; }
        public int Port { get; }
        public bool UseTls { get; }

        public NetworkEndpoint(string host, int port, bool useTls)
        {
            Host = host;
            Port = port;
            UseTls = useTls;
        }

        public override string ToString()
        {
            return $"{Host}:{Port} (TLS: {UseTls})";
        }
    }
}
