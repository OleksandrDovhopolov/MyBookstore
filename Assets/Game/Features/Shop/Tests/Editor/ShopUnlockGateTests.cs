using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Game.Conditions.API;
using Game.Conditions.Services;
using Game.Configs.Models;
using Game.Rewards.API;
using Game.Shop.API;
using Game.Shop.Services;
using Game.Shop.Tests.Editor.Fakes;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Shop.Tests.Editor
{
    /// <summary>
    /// Progression gating for shop lots (REL-10): a lot whose <c>unlock</c> condition is unmet is hidden
    /// from the storefront and unbuyable, while lots without the node stay always-available.
    /// </summary>
    public sealed class ShopUnlockGateTests
    {
        private const string Gold = "gold";
        private const string Decor = "newspaper.decor";

        // ----- fixtures -----

        private static ShopConfig Lot(string id, string storefront, JObject unlock = null) =>
            new ShopConfig
            {
                Id = id,
                StorefrontId = storefront,
                Price = new ShopPriceData { Currency = Gold, Amount = 10 },
                RewardId = "reward_" + id,
                RewardItems = new[]
                {
                    new RewardItemData
                    {
                        Id = id + "_item", Category = "decor", Amount = 1, Kind = RewardKind.InventoryItem
                    }
                },
                Limit = new ShopLotLimitData { Mode = ShopLimitMode.Disposable, MaxPurchases = 1 },
                Unlock = unlock
            };

        private static ShopConfig DecorLot(string id, JObject unlock = null) => Lot(id, Decor, unlock);

        private static JObject DayAtLeast(int min) =>
            new JObject { ["type"] = "dayAtLeast", ["min"] = min };

        private static JObject Visited(string locationId) =>
            new JObject { ["type"] = "visitLocation", ["locationId"] = locationId, ["min"] = 1 };

        private sealed class Harness
        {
            public ShopService Svc;
            public FakeSaveService Save;
            public FakeResourcesService Resources;
            public FakeConfigsService Configs;
            public FakeCurrentDayProvider Day;
            public StubDayProvider ConditionDay;
            public StubVisits Visits;
        }

        private static Harness Build(IReadOnlyList<ShopConfig> lots, bool withParser = true)
        {
            var h = new Harness
            {
                Save = new FakeSaveService(),
                Resources = new FakeResourcesService(),
                Configs = new FakeConfigsService(),
                Day = new FakeCurrentDayProvider(),
                ConditionDay = new StubDayProvider(),
                Visits = new StubVisits()
            };
            h.Configs.Seed(lots ?? new List<ShopConfig>());

            Func<IConditionParser> parser = null;
            if (withParser)
            {
                var registry = new ConditionFactoryRegistry(new IConditionFactory[]
                {
                    new StubDayAtLeastFactory(h.ConditionDay),
                    new StubVisitLocationFactory(h.Visits)
                });
                var instance = new ConditionParser(registry);
                parser = () => instance;
            }

            h.Svc = new ShopService(
                h.Save,
                new SaveBackedShopRepository(h.Save),
                h.Resources,
                new FakeRewardGrantService(),
                new ShopConfigRewardSpecProvider(h.Configs),
                h.Configs,
                new FakeInventoryService(),
                h.Day,
                null,
                parser);
            h.Svc.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            return h;
        }

        private static IEnumerable<string> OfferedIds(Harness h, string storefront = Decor) =>
            h.Svc.GetOfferedLots(storefront).Select(l => l.LotId);

        // ----- hiding -----

        [Test]
        public void GatedLot_IsHiddenFromOfferedButStillInCatalog()
        {
            var h = Build(new[] { DecorLot("open"), DecorLot("locked", DayAtLeast(5)) });
            h.ConditionDay.Day = 1;

            CollectionAssert.AreEquivalent(new[] { "open" }, OfferedIds(h).ToArray());
            CollectionAssert.AreEquivalent(new[] { "open", "locked" },
                h.Svc.GetLots(Decor).Select(l => l.LotId).ToArray());
        }

        [Test]
        public void LotWithoutUnlockNode_IsAlwaysOffered()
        {
            var h = Build(new[] { DecorLot("plain") });

            Assert.That(OfferedIds(h), Does.Contain("plain"));
            Assert.That(h.Svc.IsAvailable("plain"), Is.True);
        }

        [Test]
        public void EmptyUnlockNode_IsTreatedAsAlwaysAvailable()
        {
            var h = Build(new[] { DecorLot("plain", new JObject()) });

            Assert.That(OfferedIds(h), Does.Contain("plain"));
        }

        [Test]
        public void GatedLot_IsNotAvailableAndCannotBeBought()
        {
            var h = Build(new[] { DecorLot("locked", DayAtLeast(5)) });
            h.ConditionDay.Day = 1;
            h.Resources.Seed(Gold, 1000);

            Assert.That(h.Svc.IsAvailable("locked"), Is.False);

            var result = h.Svc.BuyAsync("locked", CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result.Status, Is.EqualTo(ShopPurchaseStatus.NotOffered));
            Assert.That(h.Svc.GetPurchaseCount("locked"), Is.Zero);
        }

        // ----- no stale cache -----

        [Test]
        public void AdvancingTheDay_RevealsTheLotWithoutRebuildingTheCatalog()
        {
            var h = Build(new[] { DecorLot("day3", DayAtLeast(3)) });
            h.ConditionDay.Day = 1;
            Assert.That(OfferedIds(h), Does.Not.Contain("day3"));

            h.ConditionDay.Day = 3;

            Assert.That(OfferedIds(h), Does.Contain("day3"), "unlock state must not be cached");
            Assert.That(h.Svc.IsAvailable("day3"), Is.True);
        }

        [Test]
        public void VisitingTheLocation_RevealsTheLotOnTheSameDay()
        {
            // The regression a day-keyed cache would cause: a visit does not change the day, so a lot
            // gated on visitLocation would stay hidden until the next morning.
            var h = Build(new[] { DecorLot("campus", Visited("loc_campus")) });
            h.ConditionDay.Day = 1;
            Assert.That(OfferedIds(h), Does.Not.Contain("campus"));

            h.Visits.Visit("loc_campus");

            Assert.That(OfferedIds(h), Does.Contain("campus"));
        }

        // ----- fail closed -----

        [Test]
        public void MalformedUnlockNode_HidesTheLot()
        {
            // ConditionParser is fail-closed and logs an error for an unregistered type.
            LogAssert.Expect(LogType.Error, new Regex("noSuchCondition"));
            var h = Build(new[] { DecorLot("broken", new JObject { ["type"] = "noSuchCondition" }) });

            Assert.That(OfferedIds(h), Does.Not.Contain("broken"));
            Assert.That(h.Svc.IsAvailable("broken"), Is.False);
        }

        [Test]
        public void UnlockNodeWithoutAParser_HidesTheLotAndLogsAnError()
        {
            LogAssert.Expect(LogType.Error, new Regex("unlock condition"));
            var h = Build(new[] { DecorLot("locked", DayAtLeast(1)) }, withParser: false);

            Assert.That(OfferedIds(h), Does.Not.Contain("locked"));
        }

        // ----- book rotation is untouched -----

        [Test]
        public void BookRotation_StillOffersFourLotsPerDay()
        {
            var books = Enumerable.Range(0, 11)
                .Select(i => Lot("book_" + i, NewspaperShopLotIds.StorefrontBooks))
                .ToArray();
            var h = Build(books);

            var offered = h.Svc.GetOfferedLots(NewspaperShopLotIds.StorefrontBooks);

            Assert.That(offered.Count, Is.EqualTo(4));
            Assert.That(h.Svc.GetLots(NewspaperShopLotIds.StorefrontBooks).Count, Is.EqualTo(11));
        }

        [Test]
        public void GatedBookLot_IsDroppedFromTheRotation()
        {
            var books = Enumerable.Range(0, 11)
                .Select(i => Lot("book_" + i, NewspaperShopLotIds.StorefrontBooks))
                .ToList();
            books.Add(Lot("book_locked", NewspaperShopLotIds.StorefrontBooks, DayAtLeast(9)));
            var h = Build(books);
            h.ConditionDay.Day = 1;

            var offered = h.Svc.GetOfferedLots(NewspaperShopLotIds.StorefrontBooks).Select(l => l.LotId);

            Assert.That(offered, Does.Not.Contain("book_locked"));
        }

        // ----- other storefronts are gated too -----

        [Test]
        public void UngatedLotOnAnUnrelatedStorefront_StaysOffered()
        {
            // Regression guard for the tutorial gift lot, which lives on its own storefront and is bought
            // directly rather than through a tab.
            var h = Build(new[] { Lot("tutorial_gift", "tutorial") });

            Assert.That(h.Svc.GetOfferedLots("tutorial").Select(l => l.LotId), Does.Contain("tutorial_gift"));
            Assert.That(h.Svc.IsAvailable("tutorial_gift"), Is.True);
        }

        // ----- stubs -----

        private sealed class StubDayProvider
        {
            public int Day = 1;
        }

        private sealed class StubVisits
        {
            private readonly HashSet<string> _visited = new(StringComparer.Ordinal);

            public void Visit(string locationId) => _visited.Add(locationId);
            public int GetVisits(string locationId) => _visited.Contains(locationId) ? 1 : 0;
        }

        private sealed class StubDayAtLeastFactory : IConditionFactory
        {
            private readonly StubDayProvider _days;

            public StubDayAtLeastFactory(StubDayProvider days) => _days = days;

            public string Type => "dayAtLeast";

            public ICondition Create(JObject node)
            {
                var min = node.Value<int?>("min") ?? 1;
                return new Cond(() => _days.Day >= min);
            }
        }

        private sealed class StubVisitLocationFactory : IConditionFactory
        {
            private readonly StubVisits _visits;

            public StubVisitLocationFactory(StubVisits visits) => _visits = visits;

            public string Type => "visitLocation";

            public ICondition Create(JObject node)
            {
                var locationId = node.Value<string>("locationId");
                if (string.IsNullOrEmpty(locationId)) throw new ArgumentException("locationId is required");
                var min = node.Value<int?>("min") ?? 1;
                return new Cond(() => _visits.GetVisits(locationId) >= min);
            }
        }

        private sealed class Cond : ICondition
        {
            private readonly Func<bool> _met;

            public Cond(Func<bool> met) => _met = met;

            public ConditionResult Evaluate() => ConditionResult.Boolean(_met(), "stub");
        }
    }
}
