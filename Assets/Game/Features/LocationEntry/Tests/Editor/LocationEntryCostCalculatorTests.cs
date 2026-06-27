using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Decor;
using Game.LocationEntry.Services;
using Game.Resources.API;
using NUnit.Framework;

namespace Game.LocationEntry.Tests.Editor
{
    public sealed class LocationEntryCostCalculatorTests
    {
        private const string Loc = "loc_a";

        private static LocationEntryCostCalculator Build(
            int entryCost, string currency = null, params (string decorId, int delta)[] activeDecor)
        {
            var configs = new FakeConfigsService()
                .Add(new LocationConfig { Id = Loc, EntryCost = entryCost, EntryCurrencyId = currency });

            var activeIds = new List<string>();
            foreach (var (decorId, delta) in activeDecor)
            {
                configs.Add(new DecorConfig { Id = decorId, VisitCostDelta = delta });
                activeIds.Add(decorId);
            }

            return new LocationEntryCostCalculator(configs, new FakeDecorPlacementService(activeIds));
        }

        [Test]
        public void BaseOnly_NoDecor()
        {
            var cost = Build(30).Calculate(Loc);
            Assert.AreEqual(30, cost.Total);
            Assert.AreEqual(30, cost.Base);
            Assert.AreEqual(0, cost.DecorDelta);
            Assert.AreEqual(ResourceIds.Gold, cost.CurrencyId);
        }

        [Test]
        public void PositiveDecorDelta_AddsToTotal()
        {
            var cost = Build(30, null, ("globe", 5), ("lamp", 3)).Calculate(Loc);
            Assert.AreEqual(8, cost.DecorDelta);
            Assert.AreEqual(38, cost.Total);
        }

        [Test]
        public void NegativeDecorDelta_DiscountsButClampsAtZero()
        {
            var cost = Build(10, null, ("discount", -25)).Calculate(Loc);
            Assert.AreEqual(-25, cost.DecorDelta);
            Assert.AreEqual(0, cost.Total, "Total must clamp to >= 0.");
        }

        [Test]
        public void UnknownLocation_ReturnsZeroGold()
        {
            var cost = Build(30).Calculate("nope");
            Assert.AreEqual(0, cost.Total);
            Assert.AreEqual(ResourceIds.Gold, cost.CurrencyId);
        }

        [Test]
        public void UnknownLocation_IgnoresActiveDecorDeltas()
        {
            var cost = Build(30, null, ("discount", -25), ("lamp", 10)).Calculate("nope");
            Assert.AreEqual(0, cost.Total);
            Assert.AreEqual(0, cost.Base);
            Assert.AreEqual(0, cost.DecorDelta);
            Assert.AreEqual(ResourceIds.Gold, cost.CurrencyId);
        }

        [Test]
        public void CustomCurrency_IsHonored()
        {
            var cost = Build(30, ResourceIds.Gems).Calculate(Loc);
            Assert.AreEqual(ResourceIds.Gems, cost.CurrencyId);
            Assert.AreEqual(30, cost.Total);
        }

        // ----- fakes -----

        private sealed class FakeDecorPlacementService : IDecorPlacementService
        {
            private readonly IReadOnlyList<string> _active;
            public FakeDecorPlacementService(IReadOnlyList<string> active) => _active = active;

            public IReadOnlyList<string> GetActiveDecorIds() => _active;

            public IReadOnlyList<DecorPlacementEntry> GetAllPlacements() => Array.Empty<DecorPlacementEntry>();
            public string GetDecorInSlot(string slotId) => null;
            public UniTask<DecorPlacementResult> PlaceAsync(string decorId, string slotId, CancellationToken ct)
                => UniTask.FromResult(DecorPlacementResult.Success);
            public UniTask UnplaceAsync(string slotId, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask ClearAllAsync(CancellationToken ct) => UniTask.CompletedTask;
            public event Action PlacementChanged { add { } remove { } }
        }

        private sealed class FakeConfigsService : IConfigsService
        {
            private readonly Dictionary<string, IConfig> _byId = new(StringComparer.OrdinalIgnoreCase);

            public FakeConfigsService Add(IConfig config)
            {
                if (config != null && !string.IsNullOrEmpty(config.Id)) _byId[config.Id] = config;
                return this;
            }

            public T Get<T>(string id) where T : class, IConfig
                => id != null && _byId.TryGetValue(id, out var cfg) ? cfg as T : null;

            public bool TryGet<T>(string id, out T config) where T : class, IConfig
            {
                config = Get<T>(id);
                return config != null;
            }

            public UniTask<T> GetAsync<T>(string id) where T : class, IConfig => UniTask.FromResult(Get<T>(id));
            public bool IsExists<T>(string id) where T : class, IConfig => Get<T>(id) != null;

            public IReadOnlyList<T> GetAll<T>() where T : class, IConfig
            {
                var list = new List<T>();
                foreach (var cfg in _byId.Values)
                    if (cfg is T typed) list.Add(typed);
                return list;
            }

            public UniTask WarmupAsync(CancellationToken ct) => UniTask.CompletedTask;
        }
    }
}
