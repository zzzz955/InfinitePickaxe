using System;
using System.Collections.Generic;
using UnityEngine;

namespace InfinitePickaxe.Client.Core
{
    public sealed class ServiceRegistry : IDisposable
    {
        private readonly Dictionary<Type, object> services = new Dictionary<Type, object>();
        private readonly List<IDisposable> disposables = new List<IDisposable>();
        private bool disposed;

        public void RegisterSingleton<TService>(TService instance) where TService : class
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(ServiceRegistry));
            }

            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            var key = typeof(TService);
            if (services.ContainsKey(key))
            {
                Debug.LogWarning($"Service {key.Name} is already registered. Overwriting.");
            }

            services[key] = instance;
            if (instance is IDisposable disposable)
            {
                disposables.Add(disposable);
            }
        }

        public TService Resolve<TService>() where TService : class
        {
            if (services.TryGetValue(typeof(TService), out var instance))
            {
                return (TService)instance;
            }

            throw new InvalidOperationException($"Service {typeof(TService).Name} is not registered.");
        }

        public bool TryResolve<TService>(out TService service) where TService : class
        {
            if (services.TryGetValue(typeof(TService), out var instance))
            {
                service = (TService)instance;
                return true;
            }

            service = null;
            return false;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            for (var i = disposables.Count - 1; i >= 0; i--)
            {
                try
                {
                    disposables[i].Dispose();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Service dispose failed: {ex}");
                }
            }

            services.Clear();
            disposables.Clear();
        }
    }
}
