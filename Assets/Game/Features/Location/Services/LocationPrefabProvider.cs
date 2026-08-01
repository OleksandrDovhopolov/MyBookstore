using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Location.API;
using Infrastructure;
using UnityEngine;

namespace Game.Location.Services
{
    public sealed class LocationPrefabProvider : ILocationPrefabProvider
    {
        public const string DefaultLocationId = "loc_park";

        private const string LogPrefix = "[LocationPrefab]";

        private readonly IConfigsService _configs;
        private readonly Func<string, CancellationToken, UniTask<GameObject>> _loadAsync;
        private readonly Dictionary<string, GameObject> _byLocationId = new();
        private readonly Dictionary<string, GameObject> _byAddress = new();
        private readonly HashSet<string> _fallbackWarnedLocationIds = new();
        private GameObject _lastPreloadedPrefab;

        public LocationPrefabProvider(IConfigsService configs)
            : this(configs, (address, ct) => ProdAddressablesWrapper.LoadAsync<GameObject>(address, ct))
        {
        }

        internal LocationPrefabProvider(
            IConfigsService configs,
            Func<string, CancellationToken, UniTask<GameObject>> loadAsync)
        {
            _configs = configs;
            _loadAsync = loadAsync ?? throw new ArgumentNullException(nameof(loadAsync));
        }

        public string LastPreloadedLocationId { get; private set; }

        public async UniTask<GameObject> PreloadAsync(string locationId, CancellationToken ct)
        {
            var id = NormalizeLocationId(locationId);

            if (TryGetCachedByLocation(id, out var cached))
            {
                LastPreloadedLocationId = id;
                _lastPreloadedPrefab = cached;
                return cached;
            }

            var address = ResolveAddress(id);
            var prefab = await TryLoadAsync(address, ct);
            var loadedOwnPrefab = prefab != null;

            if (prefab == null && !string.Equals(id, DefaultLocationId, StringComparison.Ordinal))
            {
                LogFallbackOnce(id, address);
                var defaultAddress = ResolveAddress(DefaultLocationId);
                prefab = await TryLoadAsync(defaultAddress, ct);
                if (prefab != null)
                    _byLocationId[DefaultLocationId] = prefab;
            }

            if (prefab == null)
            {
                Debug.LogError($"{LogPrefix} Failed to preload default location prefab '{DefaultLocationId}'. Scene fallback will be used.");
                _lastPreloadedPrefab = null;
                return null;
            }

            if (loadedOwnPrefab)
                _byLocationId[id] = prefab;

            LastPreloadedLocationId = id;
            _lastPreloadedPrefab = prefab;
            return prefab;
        }

        public GameObject GetPreloaded(string locationId)
        {
            var id = NormalizeLocationId(locationId);
            if (TryGetCachedByLocation(id, out var prefab))
                return prefab;

            return string.Equals(id, LastPreloadedLocationId, StringComparison.Ordinal)
                ? _lastPreloadedPrefab
                : null;
        }

        public bool IsPreloaded(string locationId)
        {
            var id = NormalizeLocationId(locationId);
            return TryGetCachedByLocation(id, out _);
        }

        internal string ResolveAddress(string locationId)
        {
            var id = NormalizeLocationId(locationId);
            if (_configs != null
                && _configs.TryGet<LocationConfig>(id, out var config)
                && !string.IsNullOrWhiteSpace(config?.LocationAddress))
            {
                return config.LocationAddress.Trim();
            }

            return $"location/{StripLocationPrefix(id)}";
        }

        internal static string NormalizeLocationId(string locationId)
            => string.IsNullOrWhiteSpace(locationId) ? DefaultLocationId : locationId.Trim();

        private bool TryGetCachedByLocation(string locationId, out GameObject prefab)
        {
            if (_byLocationId.TryGetValue(locationId, out prefab) && prefab != null)
                return true;

            _byLocationId.Remove(locationId);
            prefab = null;
            return false;
        }

        private async UniTask<GameObject> TryLoadAsync(string address, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(address))
                return null;

            address = address.Trim();
            if (_byAddress.TryGetValue(address, out var cached) && cached != null)
                return cached;

            _byAddress.Remove(address);

            try
            {
                var prefab = await _loadAsync(address, ct);
                if (prefab != null)
                    _byAddress[address] = prefab;
                return prefab;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} Addressables load failed for '{address}': {ex.Message}");
                return null;
            }
        }

        private static string StripLocationPrefix(string locationId)
            => locationId.StartsWith("loc_", StringComparison.Ordinal)
                ? locationId.Substring("loc_".Length)
                : locationId;

        private void LogFallbackOnce(string locationId, string address)
        {
            if (!_fallbackWarnedLocationIds.Add(locationId))
                return;

            Debug.LogWarning($"{LogPrefix} Failed to preload '{locationId}' at '{address}'. Falling back to '{DefaultLocationId}'.");
        }
    }
}
