using System.Threading;
using Game.Resources.API;
using Game.Resources.Services;
using Game.Resources.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Game.Resources.Tests.Editor
{
    public sealed class ResourcesServiceTests
    {
        private const string Gold = "gold";
        private const string Gems = "gems";

        private static (ResourcesService svc, FakeResourcesRepository repo, FakeSaveService save) Build()
        {
            var save = new FakeSaveService();
            var repo = new FakeResourcesRepository();
            var svc = new ResourcesService(save, repo);
            svc.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            return (svc, repo, save);
        }

        [Test]
        public void Constructor_SelfRegistersAsSaveHook()
        {
            var (svc, _, save) = Build();
            CollectionAssert.Contains(save.RegisteredHooks, svc);
        }

        [Test]
        public void Add_NewResource_CreatesEntry_FiresChanged()
        {
            var (svc, _, _) = Build();
            ResourceChangeEvent captured = null;
            svc.Changed += e => captured = e;

            svc.AddAsync(Gold, 60, "ftue", CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(60, svc.GetAmount(Gold));
            Assert.IsNotNull(captured);
            Assert.AreEqual(Gold, captured.ResourceId);
            Assert.AreEqual(0, captured.OldAmount);
            Assert.AreEqual(60, captured.NewAmount);
            Assert.AreEqual(60, captured.Delta);
            Assert.AreEqual("ftue", captured.Reason);
        }

        [Test]
        public void Add_Existing_Accumulates()
        {
            var (svc, _, _) = Build();
            svc.AddAsync(Gold, 60, "ftue", CancellationToken.None).GetAwaiter().GetResult();
            svc.AddAsync(Gold, 40, "sales", CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(100, svc.GetAmount(Gold));
        }

        [Test]
        public void Add_NonPositive_NoOp()
        {
            var (svc, repo, _) = Build();
            var savesBefore = repo.SaveCallCount;
            var events = 0;
            svc.Changed += _ => events++;

            svc.AddAsync(Gold, 0, "noop", CancellationToken.None).GetAwaiter().GetResult();
            svc.AddAsync(Gold, -10, "noop", CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(0, events);
            Assert.AreEqual(savesBefore, repo.SaveCallCount);
            Assert.AreEqual(0, svc.GetAmount(Gold));
        }

        [Test]
        public void Remove_Sufficient_Decrements_FiresChanged()
        {
            var (svc, _, _) = Build();
            svc.AddAsync(Gold, 100, "ftue", CancellationToken.None).GetAwaiter().GetResult();

            ResourceChangeEvent captured = null;
            svc.Changed += e => captured = e;

            var ok = svc.RemoveAsync(Gold, 30, "shop", CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(ok);
            Assert.AreEqual(70, svc.GetAmount(Gold));
            Assert.AreEqual(-30, captured.Delta);
        }

        [Test]
        public void Remove_Insufficient_ReturnsFalse_NoChange()
        {
            var (svc, repo, _) = Build();
            svc.AddAsync(Gold, 20, "ftue", CancellationToken.None).GetAwaiter().GetResult();
            var savesBefore = repo.SaveCallCount;
            var events = 0;
            svc.Changed += _ => events++;

            var ok = svc.RemoveAsync(Gold, 50, "shop", CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(ok);
            Assert.AreEqual(20, svc.GetAmount(Gold));
            Assert.AreEqual(savesBefore, repo.SaveCallCount);
            Assert.AreEqual(0, events);
        }

        [Test]
        public void Remove_AllTheWay_KeepsEntryAtZero()
        {
            var (svc, _, _) = Build();
            svc.AddAsync(Gold, 50, "ftue", CancellationToken.None).GetAwaiter().GetResult();
            svc.RemoveAsync(Gold, 50, "shop", CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(0, svc.GetAmount(Gold));
            Assert.IsTrue(svc.GetAll().ContainsKey(Gold), "Empty resource entries stay (UI may want to render 0).");
        }

        [Test]
        public void GetAll_ReturnsAllResources()
        {
            var (svc, _, _) = Build();
            svc.AddAsync(Gold, 60, "x", CancellationToken.None).GetAwaiter().GetResult();
            svc.AddAsync(Gems, 5, "x", CancellationToken.None).GetAwaiter().GetResult();

            var all = svc.GetAll();
            Assert.AreEqual(2, all.Count);
            Assert.AreEqual(60, all[Gold]);
            Assert.AreEqual(5, all[Gems]);
        }

        [Test]
        public void Roundtrip_PreservesAmounts()
        {
            var (svc, repo, _) = Build();
            svc.AddAsync(Gold, 80, "x", CancellationToken.None).GetAwaiter().GetResult();
            svc.AddAsync(Gems, 3, "x", CancellationToken.None).GetAwaiter().GetResult();

            // New service over the same repo.
            var svc2 = new ResourcesService(new FakeSaveService(), repo);
            svc2.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(80, svc2.GetAmount(Gold));
            Assert.AreEqual(3, svc2.GetAmount(Gems));
        }

        [Test]
        public void GetAmount_UnknownResource_ReturnsZero()
        {
            var (svc, _, _) = Build();
            Assert.AreEqual(0, svc.GetAmount("nothing_here"));
            Assert.AreEqual(0, svc.GetAmount(null));
            Assert.IsFalse(svc.Has("nothing_here", 1));
        }
    }
}
