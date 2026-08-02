using System.Collections.Generic;
using System.Threading;
using Game.Configs.Models;
using Game.LocationUnlock.API;
using Game.LocationUnlock.Services;
using Game.LocationUnlock.Tests.Editor.Fakes;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.LocationUnlock.Tests.Editor
{
    public sealed class LocationUnlockServiceTests
    {
        private const string LocA = "loc_a";   // gated by tag "A"
        private const string LocB = "loc_b";   // gated by tag "B"

        private sealed class Harness
        {
            public LocationUnlockService Service;
            public FakeLocationUnlockRepository Repo;
            public FakeSalesStatsService Sales;
            public FakeInventoryService Inventory;
            public FakeConditionParser Parser;
            public MutableCondition CondA;
            public MutableCondition CondB;
        }

        private static Harness Build(bool aMet = false, bool bMet = false, bool aHasCost = false,
            FakeLocationUnlockRepository repo = null)
        {
            var parser = new FakeConditionParser();
            var harness = new Harness
            {
                Repo = repo ?? new FakeLocationUnlockRepository(),
                Sales = new FakeSalesStatsService(),
                Inventory = new FakeInventoryService(),
                Parser = parser,
                CondA = parser.Register("A", aMet),
                CondB = parser.Register("B", bMet)
            };

            var configs = new FakeConfigsService()
                .Add(new LocationConfig
                {
                    Id = LocA,
                    Unlock = new JObject { ["tag"] = "A" },
                    UnlockCost = aHasCost
                        ? new[]
                        {
                            new LocationUnlockCostConfig { ItemId = "permit", Amount = 1 },
                            new LocationUnlockCostConfig { ItemId = "fuel_canister", Amount = 2 },
                        }
                        : null
                })
                .Add(new LocationConfig { Id = LocB, Unlock = new JObject { ["tag"] = "B" } });

            harness.Service = new LocationUnlockService(
                new FakeSaveService(), harness.Repo, configs, parser, harness.Sales, harness.Inventory);
            harness.Service.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            return harness;
        }

        [Test]
        public void Locked_WhenConditionsNotMet()
        {
            var h = Build();
            Assert.AreEqual(LocationUnlockState.Locked, h.Service.GetStatus(LocA).State);
            Assert.IsFalse(h.Service.IsUnlocked(LocA));
        }

        [Test]
        public void AutoUnlocked_WhenConditionsMetAtLoad()
        {
            var h = Build(aMet: true);
            Assert.AreEqual(LocationUnlockState.Unlocked, h.Service.GetStatus(LocA).State);
            Assert.IsTrue(h.Service.IsUnlocked(LocA), "Conditions met → opened automatically, no buy step.");
            CollectionAssert.Contains(h.Repo.Stored.UnlockedIds, LocA);
        }

        [Test]
        public void CostLocation_NotAutoUnlocked_WhenConditionsMetAtLoad()
        {
            var h = Build(aMet: true, aHasCost: true);

            Assert.AreEqual(LocationUnlockState.Locked, h.Service.GetStatus(LocA).State);
            Assert.IsFalse(h.Service.IsUnlocked(LocA));
            CollectionAssert.DoesNotContain(h.Repo.Stored.UnlockedIds, LocA);
        }

        [Test]
        public void TryUnlock_WhenConditionsNotMet_Fails()
        {
            var h = Build(aMet: false);
            var result = h.Service.TryUnlockAsync(LocA, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(UnlockResult.ConditionsNotMet, result);
            Assert.IsFalse(h.Service.IsUnlocked(LocA));
        }

        [Test]
        public void TryUnlock_WhenConditionsMet_Persists_FiresEvent()
        {
            // Conditions become met without a recompute signal, so it is not yet auto-unlocked.
            var h = Build(aMet: false);
            h.CondA.Met = true;
            string unlockedArg = null;
            h.Service.Unlocked += id => unlockedArg = id;

            var result = h.Service.TryUnlockAsync(LocA, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(UnlockResult.Ok, result);
            Assert.IsTrue(h.Service.IsUnlocked(LocA));
            Assert.AreEqual(LocationUnlockState.Unlocked, h.Service.GetStatus(LocA).State);
            Assert.AreEqual(LocA, unlockedArg);
            CollectionAssert.Contains(h.Repo.Stored.UnlockedIds, LocA);
        }

        [Test]
        public void TryUnlock_WithCost_NotEnoughItems_FailsWithoutRemoving()
        {
            var h = Build(aMet: true, aHasCost: true);
            h.Inventory.Seed("permit", "quest_item", 1);

            var result = h.Service.TryUnlockAsync(LocA, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(UnlockResult.NotEnoughItems, result);
            Assert.IsFalse(h.Service.IsUnlocked(LocA));
            Assert.AreEqual(1, h.Inventory.GetCount("permit"));
            Assert.AreEqual(0, h.Inventory.GetCount("fuel_canister"));
            Assert.AreEqual(0, h.Inventory.RemoveCalls.Count);
        }

        [Test]
        public void TryUnlock_WithCost_ConsumesItems_Persists_FiresEvent()
        {
            var h = Build(aMet: true, aHasCost: true);
            h.Inventory.Seed("permit", "quest_item", 1);
            h.Inventory.Seed("fuel_canister", "consumable", 3);
            string unlockedArg = null;
            h.Service.Unlocked += id => unlockedArg = id;

            var result = h.Service.TryUnlockAsync(LocA, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(UnlockResult.Ok, result);
            Assert.IsTrue(h.Service.IsUnlocked(LocA));
            Assert.AreEqual(LocA, unlockedArg);
            Assert.AreEqual(0, h.Inventory.GetCount("permit"));
            Assert.AreEqual(1, h.Inventory.GetCount("fuel_canister"));
            CollectionAssert.Contains(h.Repo.Stored.UnlockedIds, LocA);
            CollectionAssert.AreEqual(new[] { ("permit", 1), ("fuel_canister", 2) }, h.Inventory.RemoveCalls);
        }

        [Test]
        public void TryUnlock_WithCost_AlreadyUnlocked_DoesNotConsumeAgain()
        {
            var h = Build(aMet: true, aHasCost: true);
            h.Inventory.Seed("permit", "quest_item", 1);
            h.Inventory.Seed("fuel_canister", "consumable", 2);

            var first = h.Service.TryUnlockAsync(LocA, CancellationToken.None).GetAwaiter().GetResult();
            var second = h.Service.TryUnlockAsync(LocA, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(UnlockResult.Ok, first);
            Assert.AreEqual(UnlockResult.AlreadyUnlocked, second);
            Assert.AreEqual(2, h.Inventory.RemoveCalls.Count);
        }

        [Test]
        public void TryUnlock_AlreadyUnlocked_IsIdempotent()
        {
            var h = Build(aMet: true);   // auto-unlocked at load
            var second = h.Service.TryUnlockAsync(LocA, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(UnlockResult.AlreadyUnlocked, second);
        }

        [Test]
        public void TryUnlock_UnknownLocation_Fails()
        {
            var h = Build();
            var result = h.Service.TryUnlockAsync("nope", CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(UnlockResult.UnknownLocation, result);
        }

        [Test]
        public void Reactivity_AutoUnlocks_WhenConditionBecomesMet()
        {
            var h = Build(bMet: false);
            string unlockedArg = null;
            h.Service.Unlocked += id => unlockedArg = id;

            h.CondB.Met = true;            // a data source moved...
            h.Sales.RaiseChanged();        // ...and signals recompute

            Assert.AreEqual(LocB, unlockedArg);
            Assert.IsTrue(h.Service.IsUnlocked(LocB));
            Assert.AreEqual(LocationUnlockState.Unlocked, h.Service.GetStatus(LocB).State);
            CollectionAssert.Contains(h.Repo.Stored.UnlockedIds, LocB);
        }

        [Test]
        public void Reactivity_CostLocation_DoesNotAutoUnlock_WhenConditionBecomesMet()
        {
            var h = Build(aMet: false, aHasCost: true);
            h.Inventory.Seed("permit", "quest_item", 1);
            h.Inventory.Seed("fuel_canister", "consumable", 2);
            string unlockedArg = null;
            var changed = new List<string>();
            h.Service.Unlocked += id => unlockedArg = id;
            h.Service.StatusChanged += changed.Add;

            h.CondA.Met = true;
            h.Sales.RaiseChanged();

            Assert.IsNull(unlockedArg);
            Assert.IsFalse(h.Service.IsUnlocked(LocA));
            CollectionAssert.Contains(changed, LocA);
            CollectionAssert.DoesNotContain(h.Repo.Stored.UnlockedIds, LocA);
        }

        [Test]
        public void Reactivity_StatusChanged_ForStillLocked_OnDataChange()
        {
            var h = Build(bMet: false);
            var changed = new List<string>();
            h.Service.StatusChanged += changed.Add;

            h.Sales.RaiseChanged();   // nothing crossed the threshold; progress may have moved

            CollectionAssert.Contains(changed, LocB);
            Assert.IsFalse(h.Service.IsUnlocked(LocB));
        }

        [Test]
        public void Roundtrip_StaysUnlockedAcrossReload_EvenIfConditionsNoLongerMet()
        {
            var h = Build(aMet: true);   // auto-unlocked + persisted

            // New service instance backed by the same repository, but conditions no longer pass.
            var h2 = Build(aMet: false, repo: h.Repo);
            Assert.IsTrue(h2.Service.IsUnlocked(LocA), "Opened locations stay opened forever.");
            Assert.AreEqual(LocationUnlockState.Unlocked, h2.Service.GetStatus(LocA).State);
        }
    }
}
