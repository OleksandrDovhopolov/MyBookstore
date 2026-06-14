using System.Collections.Generic;
using System.Threading;
using Game.Inventory.API;
using Game.Inventory.Services;
using Game.Inventory.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Game.Inventory.Tests.Editor
{
    public sealed class InventoryServiceTests
    {
        private const string BookCategory = "book";
        private const string PuzzleCategory = "puzzle_piece";

        private static (InventoryService svc, FakeInventoryRepository repo, FakeSaveService save) Build()
        {
            var save = new FakeSaveService();
            var repo = new FakeInventoryRepository();
            var registry = new ItemCategoryRegistry();
            registry.Register(new ItemCategory(BookCategory, ItemStackingMode.Unique, "Books"));
            registry.Register(new ItemCategory(PuzzleCategory, ItemStackingMode.Stack, "Puzzle Pieces"));
            var svc = new InventoryService(save, repo, registry);
            // Force AfterLoadAsync to populate cache from repo.
            svc.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            return (svc, repo, save);
        }

        private static int RunSync(Cysharp.Threading.Tasks.UniTask task)
        {
            task.GetAwaiter().GetResult();
            return 0;
        }

        [Test]
        public void Constructor_SelfRegistersAsSaveHook()
        {
            var (svc, _, save) = Build();
            CollectionAssert.Contains(save.RegisteredHooks, svc);
        }

        [Test]
        public void AddUnique_AddsItem_FiresChanged()
        {
            var (svc, _, _) = Build();
            InventoryChangeEvent captured = null;
            svc.Changed += e => captured = e;

            svc.AddAsync("b1", BookCategory, 1, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(svc.Has("b1"));
            Assert.AreEqual(1, svc.GetCount("b1"));
            Assert.IsNotNull(captured);
            Assert.AreEqual(InventoryChangeKind.Added, captured.Kind);
            Assert.AreEqual(BookCategory, captured.CategoryId);
            Assert.AreEqual(1, captured.NewCount);
        }

        [Test]
        public void AddUnique_Duplicate_IsIdempotent_NoChangeEvent()
        {
            var (svc, repo, _) = Build();
            svc.AddAsync("b1", BookCategory, 1, CancellationToken.None).GetAwaiter().GetResult();

            var savesBefore = repo.SaveCallCount;
            var events = 0;
            svc.Changed += _ => events++;

            svc.AddAsync("b1", BookCategory, 1, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(savesBefore, repo.SaveCallCount, "Idempotent add must not persist again.");
            Assert.AreEqual(0, events);
        }

        [Test]
        public void AddStack_AccumulatesCount()
        {
            var (svc, _, _) = Build();
            svc.AddAsync("p1", PuzzleCategory, 2, CancellationToken.None).GetAwaiter().GetResult();
            svc.AddAsync("p1", PuzzleCategory, 3, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(5, svc.GetCount("p1"));
        }

        [Test]
        public void GetByCategory_ReturnsOnlyMatchingCategory()
        {
            var (svc, _, _) = Build();
            svc.AddAsync("b1", BookCategory, 1, CancellationToken.None).GetAwaiter().GetResult();
            svc.AddAsync("p1", PuzzleCategory, 2, CancellationToken.None).GetAwaiter().GetResult();

            var books = svc.GetByCategory(BookCategory);
            Assert.AreEqual(1, books.Count);
            Assert.AreEqual("b1", books[0].ItemId);

            var pieces = svc.GetByCategory(PuzzleCategory);
            Assert.AreEqual(1, pieces.Count);
            Assert.AreEqual(2, pieces[0].Count);
        }

        [Test]
        public void RemoveUnique_DropsEntry_FiresRemoved()
        {
            var (svc, _, _) = Build();
            svc.AddAsync("b1", BookCategory, 1, CancellationToken.None).GetAwaiter().GetResult();

            InventoryChangeEvent captured = null;
            svc.Changed += e => captured = e;

            var ok = svc.RemoveAsync("b1", 1, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(ok);
            Assert.IsFalse(svc.Has("b1"));
            Assert.AreEqual(InventoryChangeKind.Removed, captured.Kind);
        }

        [Test]
        public void RemoveStack_PartialAmount_Decrements()
        {
            var (svc, _, _) = Build();
            svc.AddAsync("p1", PuzzleCategory, 5, CancellationToken.None).GetAwaiter().GetResult();

            var ok = svc.RemoveAsync("p1", 2, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(ok);
            Assert.AreEqual(3, svc.GetCount("p1"));
        }

        [Test]
        public void RemoveStack_FullAmount_DropsEntry()
        {
            var (svc, _, _) = Build();
            svc.AddAsync("p1", PuzzleCategory, 3, CancellationToken.None).GetAwaiter().GetResult();

            var ok = svc.RemoveAsync("p1", 3, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(ok);
            Assert.IsFalse(svc.Has("p1"));
        }

        [Test]
        public void RemoveStack_MoreThanOwned_Fails_NoChange()
        {
            var (svc, _, _) = Build();
            svc.AddAsync("p1", PuzzleCategory, 2, CancellationToken.None).GetAwaiter().GetResult();

            var ok = svc.RemoveAsync("p1", 5, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(ok);
            Assert.AreEqual(2, svc.GetCount("p1"));
        }

        [Test]
        public void AddBatch_PersistsOnce()
        {
            var (svc, repo, _) = Build();
            var savesBefore = repo.SaveCallCount;

            var items = new List<InventoryItem>
            {
                new("b1", BookCategory, 1),
                new("b2", BookCategory, 1),
                new("p1", PuzzleCategory, 4)
            };
            svc.AddBatchAsync(items, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(savesBefore + 1, repo.SaveCallCount, "Batch must write to repo once total.");
            Assert.IsTrue(svc.Has("b1"));
            Assert.IsTrue(svc.Has("b2"));
            Assert.AreEqual(4, svc.GetCount("p1"));
        }

        [Test]
        public void Roundtrip_PreservesUniquesAndStacks()
        {
            var (svc, repo, _) = Build();
            svc.AddAsync("b1", BookCategory, 1, CancellationToken.None).GetAwaiter().GetResult();
            svc.AddAsync("p1", PuzzleCategory, 7, CancellationToken.None).GetAwaiter().GetResult();

            // Build a new service over the same repo to simulate restart.
            var registry = new ItemCategoryRegistry();
            registry.Register(new ItemCategory(BookCategory, ItemStackingMode.Unique, "Books"));
            registry.Register(new ItemCategory(PuzzleCategory, ItemStackingMode.Stack, "Puzzle Pieces"));
            var svc2 = new InventoryService(new FakeSaveService(), repo, registry);
            svc2.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(svc2.Has("b1"));
            Assert.AreEqual(7, svc2.GetCount("p1"));
        }

        [Test]
        public void AddWithUnknownCategory_NoOp()
        {
            var (svc, repo, _) = Build();
            var savesBefore = repo.SaveCallCount;

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex(@"\[Inventory\] Add rejected: unknown category"));

            svc.AddAsync("x1", "no_such_category", 1, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(svc.Has("x1"));
            Assert.AreEqual(savesBefore, repo.SaveCallCount);
        }
    }
}
