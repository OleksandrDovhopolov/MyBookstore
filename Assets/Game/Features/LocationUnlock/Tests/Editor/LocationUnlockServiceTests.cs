using System.Threading;
using Game.Configs.Models;
using Game.LocationUnlock.API;
using Game.LocationUnlock.Services;
using Game.LocationUnlock.Tests.Editor.Fakes;
using Game.Resources.API;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.LocationUnlock.Tests.Editor
{
    public sealed class LocationUnlockServiceTests
    {
        private const string LocA = "loc_a";   // cost 100, gated by tag "A"
        private const string LocB = "loc_b";   // cost 0,   gated by tag "B"

        private sealed class Harness
        {
            public LocationUnlockService Service;
            public FakeLocationUnlockRepository Repo;
            public FakeResourcesService Resources;
            public FakeSalesStatsService Sales;
            public FakeConditionParser Parser;
            public MutableCondition CondA;
            public MutableCondition CondB;
        }

        private static Harness Build(int gold = 1000, bool aMet = false, bool bMet = false,
            FakeLocationUnlockRepository repo = null)
        {
            var parser = new FakeConditionParser();
            var harness = new Harness
            {
                Repo = repo ?? new FakeLocationUnlockRepository(),
                Resources = new FakeResourcesService(),
                Sales = new FakeSalesStatsService(),
                Parser = parser,
                CondA = parser.Register("A", aMet),
                CondB = parser.Register("B", bMet)
            };
            harness.Resources.Set(ResourceIds.Gold, gold);

            var configs = new FakeConfigsService()
                .Add(new LocationConfig { Id = LocA, UnlockCost = 100, Unlock = new JObject { ["tag"] = "A" } })
                .Add(new LocationConfig { Id = LocB, UnlockCost = 0, Unlock = new JObject { ["tag"] = "B" } });

            harness.Service = new LocationUnlockService(
                new FakeSaveService(), harness.Repo, configs, parser, harness.Resources, harness.Sales);
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
        public void Unlockable_WhenConditionsMet_ButNotPurchased()
        {
            var h = Build(aMet: true);
            var status = h.Service.GetStatus(LocA);
            Assert.AreEqual(LocationUnlockState.Unlockable, status.State);
            Assert.AreEqual(100, status.UnlockCost);
            Assert.IsFalse(h.Service.IsUnlocked(LocA), "Conditions met is not the same as purchased.");
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
        public void TryUnlock_SpendsGold_Persists_FiresEvent()
        {
            var h = Build(gold: 1000, aMet: true);
            string unlockedArg = null;
            h.Service.Unlocked += id => unlockedArg = id;

            var result = h.Service.TryUnlockAsync(LocA, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(UnlockResult.Ok, result);
            Assert.IsTrue(h.Service.IsUnlocked(LocA));
            Assert.AreEqual(LocationUnlockState.Unlocked, h.Service.GetStatus(LocA).State);
            Assert.AreEqual(900, h.Resources.GetAmount(ResourceIds.Gold));
            Assert.AreEqual(LocA, unlockedArg);
            CollectionAssert.Contains(h.Repo.Stored.UnlockedIds, LocA);
        }

        [Test]
        public void TryUnlock_AlreadyUnlocked_IsIdempotent()
        {
            var h = Build(aMet: true);
            h.Service.TryUnlockAsync(LocA, CancellationToken.None).GetAwaiter().GetResult();

            var second = h.Service.TryUnlockAsync(LocA, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(UnlockResult.AlreadyUnlocked, second);
            Assert.AreEqual(900, h.Resources.GetAmount(ResourceIds.Gold), "Must not charge twice.");
        }

        [Test]
        public void TryUnlock_NotEnoughGold_Fails()
        {
            var h = Build(gold: 50, aMet: true);
            var result = h.Service.TryUnlockAsync(LocA, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(UnlockResult.NotEnoughCurrency, result);
            Assert.IsFalse(h.Service.IsUnlocked(LocA));
            Assert.AreEqual(50, h.Resources.GetAmount(ResourceIds.Gold));
        }

        [Test]
        public void TryUnlock_ZeroCost_UnlocksWithoutCharge()
        {
            var h = Build(gold: 0, bMet: true);
            var result = h.Service.TryUnlockAsync(LocB, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(UnlockResult.Ok, result);
            Assert.IsTrue(h.Service.IsUnlocked(LocB));
        }

        [Test]
        public void TryUnlock_UnknownLocation_Fails()
        {
            var h = Build();
            var result = h.Service.TryUnlockAsync("nope", CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(UnlockResult.UnknownLocation, result);
        }

        [Test]
        public void Reactivity_StatusChanged_WhenConditionBecomesMet()
        {
            var h = Build(bMet: false);
            string changed = null;
            h.Service.StatusChanged += id => changed = id;

            h.CondB.Met = true;            // a data source moved...
            h.Sales.RaiseChanged();        // ...and signals recompute

            Assert.AreEqual(LocB, changed);
            Assert.AreEqual(LocationUnlockState.Unlockable, h.Service.GetStatus(LocB).State);
        }

        [Test]
        public void Reactivity_NoStatusChanged_WhenStateUnchanged()
        {
            var h = Build(bMet: false);
            var fired = 0;
            h.Service.StatusChanged += _ => fired++;

            h.Sales.RaiseChanged();   // nothing moved

            Assert.AreEqual(0, fired);
        }

        [Test]
        public void Roundtrip_UnlockedPersistsAcrossReload()
        {
            var h = Build(aMet: true);
            h.Service.TryUnlockAsync(LocA, CancellationToken.None).GetAwaiter().GetResult();

            // New service instance backed by the same repository.
            var h2 = Build(aMet: true, repo: h.Repo);
            Assert.IsTrue(h2.Service.IsUnlocked(LocA));
            Assert.AreEqual(LocationUnlockState.Unlocked, h2.Service.GetStatus(LocA).State);
        }
    }
}
