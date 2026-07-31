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
        private readonly Dictionary<string, GameObject> _byLocationId = new();
        private readonly Dictionary<string, GameObject> _byAddress = new();

        public LocationPrefabProvider(IConfigsService configs)
        {
            _configs = configs;
        }

        public string LastPreloadedLocationId { get; private set; }

        public async UniTask<GameObject> PreloadAsync(string locationId, CancellationToken ct)
        {
            var id = NormalizeLocationId(locationId);

            if (TryGetCachedByLocation(id, out var cached))
            {
                LastPreloadedLocationId = id;
                return cached;
            }

            var address = ResolveAddress(id);
            var prefab = await TryLoadAsync(address, ct);

            if (prefab == null && !string.Equals(id, DefaultLocationId, StringComparison.Ordinal))
            {
                Debug.LogWarning($"{LogPrefix} Failed to preload '{id}' at '{address}'. Falling back to '{DefaultLocationId}'.");
                prefab = await TryLoadAsync(ResolveAddress(DefaultLocationId), ct);
            }

            if (prefab == null)
            {
                Debug.LogError($"{LogPrefix} Failed to preload default location prefab '{DefaultLocationId}'. Scene fallback will be used.");
                return null;
            }

            _byLocationId[id] = prefab;
            LastPreloadedLocationId = id;
            return prefab;
        }

        public GameObject GetPreloaded(string locationId)
        {
            var id = NormalizeLocationId(locationId);
            return TryGetCachedByLocation(id, out var prefab) ? prefab : null;
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
                var prefab = await ProdAddressablesWrapper.LoadAsync<GameObject>(address, ct);
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
    }
}
