using System;
using InfinitePickaxe.Client.Config;
using UnityEngine;

namespace InfinitePickaxe.Client.Core
{
    public static class ClientRuntime
    {
        private static readonly object SyncRoot = new object();
        private static bool initialized;
        private static ServiceRegistry registry;
        private static ClientConfigData config;

        public static bool IsInitialized => initialized;
        public static ClientConfigData Config => config;
        public static ServiceRegistry Services => registry;

        public static void Initialize(ClientConfigData configData, Action<ServiceRegistry> configure = null)
        {
            lock (SyncRoot)
            {
                if (initialized)
                {
                    Debug.LogWarning("ClientRuntime.Initialize called more than once. Ignoring subsequent call.");
                    return;
                }

                config = configData ?? ClientConfigData.Default();
                registry = new ServiceRegistry();
                registry.RegisterSingleton(config);
                configure?.Invoke(registry);
                initialized = true;
                Debug.Log($"ClientRuntime initialized for env '{config.environment}'.");
            }
        }

        public static TService Resolve<TService>() where TService : class
        {
            EnsureInitialized();
            return registry.Resolve<TService>();
        }

        public static bool TryResolve<TService>(out TService service) where TService : class
        {
            if (!initialized)
            {
                service = null;
                return false;
            }

            return registry.TryResolve(out service);
        }

        public static void Dispose()
        {
            lock (SyncRoot)
            {
                if (!initialized)
                {
                    return;
                }

                initialized = false;
                try
                {
                    registry.Dispose();
                }
                finally
                {
                    registry = null;
                    config = null;
                }
            }
        }

        private static void EnsureInitialized()
        {
            if (!initialized)
            {
                throw new InvalidOperationException("ClientRuntime is not initialized. Call ClientRuntime.Initialize first.");
            }
        }
    }
}
