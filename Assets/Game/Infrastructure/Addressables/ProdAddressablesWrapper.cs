using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace Infrastructure
{
    public static class ProdAddressablesWrapper
    {
        private readonly struct CacheKey : IEquatable<CacheKey>
        {
            public readonly string Address;
            public readonly Type AssetType;

            public CacheKey(string address, Type assetType)
            {
                Address = address;
                AssetType = assetType;
            }

            public bool Equals(CacheKey other) => Address == other.Address && AssetType == other.AssetType;
            public override bool Equals(object obj) => obj is CacheKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Address != null ? Address.GetHashCode() : 0) * 397) ^ (AssetType != null ? AssetType.GetHashCode() : 0);
                }
            }
        }

        private static readonly Dictionary<CacheKey, AsyncOperationHandle> HandleByKey = new();
        private static readonly Dictionary<CacheKey, int> RefCountByKey = new();
        private static readonly Dictionary<int, CacheKey> InstanceIdToKey = new();
        private static readonly object Lock = new();

        public static T LoadSync<T>(string address) where T : Object
        {
            if (string.IsNullOrEmpty(address))
                throw new ArgumentException("Address is null or empty.", nameof(address));

            var (key, handle) = AcquireHandle<T>(address);

            try
            {
                handle.WaitForCompletion();
            }
            catch
            {
                Release(key);
                throw;
            }

            return CompleteLoad<T>(address, key, handle);
        }

        public static async UniTask<T> LoadAsync<T>(string address, CancellationToken ct) where T : Object
        {
            if (string.IsNullOrEmpty(address))
                throw new ArgumentException("Address is null or empty.", nameof(address));

            ct.ThrowIfCancellationRequested();

            var (key, handle) = AcquireHandle<T>(address);

            try
            {
                await handle.ToUniTask(cancellationToken: ct);
            }
            catch
            {
                Release(key);
                throw;
            }

            return CompleteLoad<T>(address, key, handle);
        }

        private static (CacheKey key, AsyncOperationHandle handle) AcquireHandle<T>(string address) where T : Object
        {
            var key = new CacheKey(address, typeof(T));
            lock (Lock)
            {
                if (HandleByKey.TryGetValue(key, out var cachedHandle))
                {
                    if (!cachedHandle.IsValid())
                    {
                        HandleByKey.Remove(key);
                        RefCountByKey.Remove(key);
                    }
                    else
                    {
                        if (!RefCountByKey.ContainsKey(key))
                        {
                            RefCountByKey[key] = 0;
                        }

                        RefCountByKey[key]++;
                        return (key, cachedHandle);
                    }
                }

                var handle = Addressables.LoadAssetAsync<T>(address);
                HandleByKey[key] = handle;
                RefCountByKey[key] = 1;
                return (key, handle);
            }
        }

        private static T CompleteLoad<T>(string address, CacheKey key, AsyncOperationHandle handle) where T : Object
        {
            if (!handle.IsValid())
            {
                Release(key);
                throw new InvalidOperationException($"Invalid handle while loading address '{address}' as {typeof(T).Name}.");
            }

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Release(key);
                throw new InvalidOperationException($"Failed to load address '{address}' as {typeof(T).Name}. Status: {handle.Status}");
            }

            var result = handle.Convert<T>().Result;
            if (result == null)
            {
                Release(key);
                throw new InvalidOperationException($"Loaded null from address '{address}' as {typeof(T).Name}.");
            }

            lock (Lock)
            {
                if (!RefCountByKey.ContainsKey(key))
                    return result;

                InstanceIdToKey[result.GetInstanceID()] = key;
            }

            return result;
        }

        public static void Release(object obj)
        {
            if (obj == null)
                return;

            CacheKey key = default;
            var hasKnownKey = false;
            lock (Lock)
            {
                if (obj is Object unityObject && unityObject != null &&
                    InstanceIdToKey.TryGetValue(unityObject.GetInstanceID(), out key))
                {
                    hasKnownKey = true;
                }
            }

            if (hasKnownKey)
            {
                Release(key);
                return;
            }

            Addressables.Release(obj);
        }

        public static void Release(string address)
        {
            if (string.IsNullOrEmpty(address))
                return;

            List<CacheKey> keysToRelease;
            lock (Lock)
            {
                keysToRelease = HandleByKey.Keys
                    .Where(k => k.Address == address)
                    .ToList();
            }

            foreach (var key in keysToRelease)
            {
                Release(key);
            }
        }

        public static void ReleaseAll()
        {
            List<AsyncOperationHandle> handlesToRelease;
            lock (Lock)
            {
                handlesToRelease = HandleByKey.Values.ToList();
                HandleByKey.Clear();
                RefCountByKey.Clear();
                InstanceIdToKey.Clear();
            }

            foreach (var handle in handlesToRelease)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
        }

        private static void Release(CacheKey key)
        {
            AsyncOperationHandle handle = default;
            var shouldReleaseHandle = false;

            lock (Lock)
            {
                if (!RefCountByKey.TryGetValue(key, out var refCount))
                    return;

                refCount--;
                if (refCount > 0)
                {
                    RefCountByKey[key] = refCount;
                    return;
                }

                RefCountByKey.Remove(key);
                if (!HandleByKey.TryGetValue(key, out handle))
                    return;

                HandleByKey.Remove(key);
                if (handle.IsValid() && handle.Result is Object loadedObject && loadedObject != null)
                {
                    InstanceIdToKey.Remove(loadedObject.GetInstanceID());
                }

                shouldReleaseHandle = handle.IsValid();
            }

            if (shouldReleaseHandle)
            {
                Addressables.Release(handle);
            }
        }

        public static async UniTask LoadGroupAsync<T>(
            IEnumerable<string> addresses,
            CancellationToken ct,
            int maxConcurrency = 8) where T : Object
        {
            if (addresses == null) throw new ArgumentNullException(nameof(addresses));
            if (maxConcurrency <= 0) throw new ArgumentOutOfRangeException(nameof(maxConcurrency));

            var unique = addresses
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Distinct()
                .ToArray();

            var semaphore = new SemaphoreSlim(maxConcurrency);

            try
            {
                var tasks = unique.Select(async address =>
                {
                    ct.ThrowIfCancellationRequested();
                    await semaphore.WaitAsync(ct);
                    try
                    {
                        await LoadAsync<T>(address, ct);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }).ToArray();

                await UniTask.WhenAll(tasks);
            }
            finally
            {
                semaphore.Dispose();
            }
        }

        public static async UniTask DownloadDependenciesByLabelAsync(string label, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("Label is null or empty.", nameof(label));

            ct.ThrowIfCancellationRequested();

            var handle = Addressables.DownloadDependenciesAsync(label);
            try
            {
                await handle.ToUniTask(cancellationToken: ct);
                if (handle.Status != AsyncOperationStatus.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Addressables.DownloadDependenciesAsync failed for label '{label}'. Status: {handle.Status}");
                }
            }
            finally
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
        }

        public static async UniTask<IReadOnlyList<string>> ResolveAddressesByLabelAsync<T>(string label, CancellationToken ct)
            where T : Object
        {
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("Label is null or empty.", nameof(label));

            ct.ThrowIfCancellationRequested();

            var handle = Addressables.LoadResourceLocationsAsync(label, typeof(T));
            try
            {
                await handle.ToUniTask(cancellationToken: ct);
                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                    return Array.Empty<string>();

                return handle.Result
                    .Where(x => x != null)
                    .Select(x => x.PrimaryKey)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToArray();
            }
            finally
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
        }

        public static async UniTask<IReadOnlyList<string>> WarmupGroupByLabelAsync<T>(
            string label,
            CancellationToken ct,
            int takeCount,
            int maxConcurrency = 8) where T : Object
        {
            if (takeCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(takeCount));

            var addresses = await ResolveAddressesByLabelAsync<T>(label, ct);
            var selected = addresses.Take(takeCount).ToArray();
            if (selected.Length == 0)
                return selected;

            await LoadGroupAsync<T>(selected, ct, maxConcurrency);
            return selected;
        }

        public static void ReleaseGroup(IEnumerable<string> addresses)
        {
            if (addresses == null) return;

            foreach (var address in addresses.Where(a => !string.IsNullOrWhiteSpace(a)).Distinct())
            {
                Release(address);
            }
        }
    }
}
