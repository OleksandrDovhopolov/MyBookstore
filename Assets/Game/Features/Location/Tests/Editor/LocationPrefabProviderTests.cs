using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Location.Services;
using NUnit.Framework;
using UnityEngine;

namespace Game.Location.Tests.Editor
{
    public sealed class LocationPrefabProviderTests
    {
        [Test]
        public void ResolveAddress_UsesTrimmedLocationAddress_WhenConfigured()
        {
            var provider = new LocationPrefabProvider(new FakeConfigsService(
                new LocationConfig { Id = "loc_park", LocationAddress = "  location/custom_park  " }));

            Assert.That(provider.ResolveAddress("loc_park"), Is.EqualTo("location/custom_park"));
        }

        [TestCase("loc_park", "location/park")]
        [TestCase("park", "location/park")]
        [TestCase(" loc_campus ", "location/campus")]
        public void ResolveAddress_FallsBackToConvention(string locationId, string expected)
        {
            var provider = new LocationPrefabProvider(new FakeConfigsService(
                new LocationConfig { Id = "loc_campus", LocationAddress = " " }));

            Assert.That(provider.ResolveAddress(locationId), Is.EqualTo(expected));
        }

        [Test]
        public void NormalizeLocationId_EmptyUsesDefaultLocation()
        {
            Assert.That(LocationPrefabProvider.NormalizeLocationId(null), Is.EqualTo(LocationPrefabProvider.DefaultLocationId));
            Assert.That(LocationPrefabProvider.NormalizeLocationId(" "), Is.EqualTo(LocationPrefabProvider.DefaultLocationId));
        }

        [Test]
        public async Task PreloadAsync_FallbackDoesNotCacheUnderRequestedLocationId()
        {
            var parkPrefab = new GameObject("park prefab");
            var campusPrefab = new GameObject("campus prefab");
            var campusLoadCount = 0;

            try
            {
                var provider = new LocationPrefabProvider(
                    new FakeConfigsService(),
                    (address, _) =>
                    {
                        if (address == "location/campus")
                        {
                            campusLoadCount++;
                            return UniTask.FromResult(campusLoadCount == 1 ? null : campusPrefab);
                        }

                        return UniTask.FromResult(address == "location/park" ? parkPrefab : null);
                    });

                var first = await provider.PreloadAsync("loc_campus", CancellationToken.None);

                Assert.That(first, Is.SameAs(parkPrefab));
                Assert.That(provider.GetPreloaded("loc_campus"), Is.SameAs(parkPrefab));
                Assert.That(provider.IsPreloaded("loc_campus"), Is.False);
                Assert.That(provider.IsPreloaded("loc_park"), Is.True);

                var second = await provider.PreloadAsync("loc_campus", CancellationToken.None);

                Assert.That(second, Is.SameAs(campusPrefab));
                Assert.That(provider.GetPreloaded("loc_campus"), Is.SameAs(campusPrefab));
                Assert.That(campusLoadCount, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(parkPrefab);
                Object.DestroyImmediate(campusPrefab);
            }
        }

        private sealed class FakeConfigsService : IConfigsService
        {
            private readonly Dictionary<string, LocationConfig> _locations = new();

            public FakeConfigsService(params LocationConfig[] locations)
            {
                foreach (var location in locations)
                    _locations[location.Id] = location;
            }

            public UniTask WarmupAsync(CancellationToken ct) => UniTask.CompletedTask;
            public T Get<T>(string id) where T : class, IConfig => TryGet<T>(id, out var config) ? config : null;
            public UniTask<T> GetAsync<T>(string id) where T : class, IConfig => UniTask.FromResult(Get<T>(id));
            public bool IsExists<T>(string id) where T : class, IConfig => TryGet<T>(id, out _);
            public IReadOnlyList<T> GetAll<T>() where T : class, IConfig => System.Array.Empty<T>();

            public bool TryGet<T>(string id, out T config) where T : class, IConfig
            {
                if (typeof(T) == typeof(LocationConfig) && id != null && _locations.TryGetValue(id, out var location))
                {
                    config = location as T;
                    return true;
                }

                config = null;
                return false;
            }
        }
    }
}
