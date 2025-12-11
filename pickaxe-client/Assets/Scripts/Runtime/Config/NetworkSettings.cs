using System;
using InfinitePickaxe.Client.Net;

namespace InfinitePickaxe.Client.Config
{
    public sealed class NetworkSettings
    {
        public NetworkEndpoint Endpoint { get; }
        public TimeSpan ConnectTimeout { get; }
        public TimeSpan ReceiveTimeout { get; }
        public TimeSpan SendTimeout { get; }
        public TimeSpan HeartbeatInterval { get; }
        public int MaxMessageBytes { get; }

        public NetworkSettings(
            NetworkEndpoint endpoint,
            TimeSpan connectTimeout,
            TimeSpan receiveTimeout,
            TimeSpan sendTimeout,
            TimeSpan heartbeatInterval,
            int maxMessageBytes)
        {
            Endpoint = endpoint;
            ConnectTimeout = connectTimeout;
            ReceiveTimeout = receiveTimeout;
            SendTimeout = sendTimeout;
            HeartbeatInterval = heartbeatInterval;
            MaxMessageBytes = maxMessageBytes;
        }

        public static NetworkSettings FromConfig(ClientConfigData config)
        {
            var data = config ?? ClientConfigData.Default();
            var env = data.GetActiveEnvironment();
            var endpoint = new NetworkEndpoint(env.host, env.port, env.useTls);

            return new NetworkSettings(
                endpoint,
                TimeSpan.FromSeconds(data.connectTimeoutSeconds),
                TimeSpan.FromSeconds(data.receiveTimeoutSeconds),
                TimeSpan.FromSeconds(data.sendTimeoutSeconds),
                TimeSpan.FromSeconds(data.heartbeatIntervalSeconds),
                data.maxMessageBytes);
        }
    }
}
