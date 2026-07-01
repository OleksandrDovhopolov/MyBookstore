using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Conditions.API;
using Game.Conditions.Services;
using Game.Decor;
using Game.Decor.Conditions;
using Game.Inventory.API;
using Game.Inventory.Conditions;
using Game.DayCycle.Conditions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Quest.Tests.ConditionsIntegration
{
    /// <summary>
    /// Integration: the three new leaf factories (decorEquipped + haveItem + weatherIs) live in one
    /// <see cref="ConditionFactoryRegistry"/> and compose through the real <see cref="ConditionParser"/>
    /// under an <c>all</c> node — proving engine compatibility together, not just in isolation. Mirrors a
    /// quest task tree like "The Case of the Hideous Mascot" snowy-confession step.
    /// </summary>
    public sealed class QuestConditionsCompositionTests
    {
        private const string Knife = "knife";
        private const string KnifeWall = "knife_wall";

        private static readonly JObject Tree = new JObject
        {
            ["all"] = new JArray
            {
                new JObject { ["type"] = HaveItemConditionFactory.TypeId, ["itemId"] = Knife, ["min"] = 1 },
                new JObject { ["type"] = DecorEquippedConditionFactory.TypeId, ["decorId"] = KnifeWall },
                new JObject { ["type"] = WeatherIsConditionFactory.TypeId, ["weatherId"] = "snow" }
            }
        };

        private static ICondition Build(FakeInventory inv, FakeDecor decor, FakeWeather weather)
        {
            var registry = new ConditionFactoryRegistry(new IConditionFactory[]
            {
                new HaveItemConditionFactory(inv),
                new DecorEquippedConditionFactory(decor),
                new WeatherIsConditionFactory(() => weather)
            });
            return new ConditionParser(registry).Parse(Tree);
        }

        [Test]
        public void AllThree_MustHold_ForCompositeToBeMet()
        {
            var inv = new FakeInventory();
            var decor = new FakeDecor();
            var weather = new FakeWeather { WeatherId = "clear" };
            var condition = Build(inv, decor, weather);

            Assert.IsFalse(condition.Evaluate().IsMet, "nothing satisfied");

            inv.Counts[Knife] = 1;
            Assert.IsFalse(condition.Evaluate().IsMet, "decor + weather still missing");

            decor.Active.Add(KnifeWall);
            Assert.IsFalse(condition.Evaluate().IsMet, "weather still wrong");

            weather.WeatherId = "snow";
            var result = condition.Evaluate();
            Assert.IsTrue(result.IsMet, "all three satisfied");
            Assert.AreEqual(3, result.Current, "composite reports met-children count");
            Assert.AreEqual(3, result.Target);
        }

        // ----- fakes (each seam minimal) -----

        private sealed class FakeInventory : IInventoryService
        {
            public readonly Dictionary<string, int> Counts = new(StringComparer.Ordinal);
            public bool Has(string itemId) => GetCount(itemId) > 0;
            public int GetCount(string itemId) => itemId != null && Counts.TryGetValue(itemId, out var c) ? c : 0;
            public IReadOnlyList<InventoryItem> GetAll() => Array.Empty<InventoryItem>();
            public IReadOnlyList<InventoryItem> GetByCategory(string categoryId) => Array.Empty<InventoryItem>();
            public UniTask AddAsync(string itemId, string categoryId, int amount, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask AddBatchAsync(IEnumerable<InventoryItem> items, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask<bool> RemoveAsync(string itemId, int amount, CancellationToken ct) => UniTask.FromResult(false);
#pragma warning disable CS0067
            public event Action<InventoryChangeEvent> Changed;
#pragma warning restore CS0067
        }

        private sealed class FakeDecor : IDecorPlacementService
        {
            public readonly List<string> Active = new();
            public IReadOnlyList<string> GetActiveDecorIds() => Active;
            public IReadOnlyList<DecorPlacementEntry> GetAllPlacements() => Array.Empty<DecorPlacementEntry>();
            public string GetDecorInSlot(string slotId) => null;
            public UniTask<DecorPlacementResult> PlaceAsync(string decorId, string slotId, CancellationToken ct) => default;
            public UniTask UnplaceAsync(string slotId, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask ClearAllAsync(CancellationToken ct) => UniTask.CompletedTask;
#pragma warning disable CS0067
            public event Action PlacementChanged;
#pragma warning restore CS0067
        }

        private sealed class FakeWeather : ICurrentDayWeatherProvider
        {
            public string WeatherId = string.Empty;
            public string GetCurrentWeatherId() => WeatherId;
        }
    }
}
