using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Game.Decor;
using Game.Inventory.API;
using Game.SalesStats.API;
using Game.Tutorial.API;
using NUnit.Framework;

namespace Game.Bootstrap.Tests.Editor
{
    public sealed class TutorialReevaluationBridgeTests
    {
        [Test]
        public void SalesStatsChange_RequestsReevaluation()
        {
            var gate = new FakeGate();
            var sales = new FakeSalesStatsService();
            var bridge = new TutorialReevaluationBridge(gate, sales: sales);

            bridge.Start();
            sales.RaiseChanged();

            Assert.AreEqual(1, gate.RequestCount);
            bridge.Dispose();
        }

        [Test]
        public void InventoryChange_RequestsReevaluation()
        {
            var gate = new FakeGate();
            var inventory = new FakeInventoryService();
            var bridge = new TutorialReevaluationBridge(gate, inventory: inventory);

            bridge.Start();
            inventory.RaiseChanged();

            Assert.AreEqual(1, gate.RequestCount);
            bridge.Dispose();
        }

        [Test]
        public void DecorPlacementChange_RequestsReevaluation()
        {
            var gate = new FakeGate();
            var decor = new FakeDecorPlacementService();
            var bridge = new TutorialReevaluationBridge(gate, decor: decor);

            bridge.Start();
            decor.RaiseChanged();

            Assert.AreEqual(1, gate.RequestCount);
            bridge.Dispose();
        }

        [Test]
        public void Dispose_UnsubscribesFromSources()
        {
            var gate = new FakeGate();
            var sales = new FakeSalesStatsService();
            var inventory = new FakeInventoryService();
            var decor = new FakeDecorPlacementService();
            var bridge = new TutorialReevaluationBridge(gate, sales, inventory, decor);

            bridge.Start();
            bridge.Dispose();

            sales.RaiseChanged();
            inventory.RaiseChanged();
            decor.RaiseChanged();

            Assert.AreEqual(0, gate.RequestCount);
        }

        [Test]
        public void NullOptionalSources_StartAndDisposeWithoutThrowing()
        {
            var bridge = new TutorialReevaluationBridge(new FakeGate());

            Assert.DoesNotThrow(() =>
            {
                bridge.Start();
                bridge.Dispose();
            });
        }

        private sealed class FakeGate : ITutorialReevaluationGate
        {
            public int RequestCount { get; private set; }
            public void RequestReevaluation() => RequestCount++;
        }

        private sealed class FakeSalesStatsService : ISalesStatsService
        {
            public event Action<SalesStatsChange> Changed;

            public int TotalSold => 0;
            public int GetSold(BookGenre genre) => 0;
            public int GetSold(BookGenre genre, string locationId) => 0;
            public int GetSoldOnDay(int day) => 0;
            public int GetSoldOnDay(int day, BookGenre genre) => 0;
            public int GetMaxSoldInSingleDay(BookGenre genre) => 0;
            public int GetExcellentPicks(BookGenre genre) => 0;
            public void RecordSold(string bookId) { }
            public void RecordSold(string bookId, in SaleContext ctx) { }
            public void RecordActivePick(string bookId, in SaleContext ctx) { }

            public void RaiseChanged()
                => Changed?.Invoke(new SalesStatsChange(BookGenre.Crime, 1, 1, "book_crime"));
        }

        private sealed class FakeInventoryService : IInventoryService
        {
            public event Action<InventoryChangeEvent> Changed;

            public IReadOnlyList<InventoryItem> GetAll() => Array.Empty<InventoryItem>();
            public IReadOnlyList<InventoryItem> GetByCategory(string categoryId) => Array.Empty<InventoryItem>();
            public bool Has(string itemId) => false;
            public int GetCount(string itemId) => 0;
            public UniTask AddAsync(string itemId, string categoryId, int amount, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask AddBatchAsync(IEnumerable<InventoryItem> items, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask<bool> RemoveAsync(string itemId, int amount, CancellationToken ct) => UniTask.FromResult(false);

            public void RaiseChanged()
                => Changed?.Invoke(new InventoryChangeEvent(InventoryCategories.Decor, "globe", InventoryChangeKind.Added, 1));
        }

        private sealed class FakeDecorPlacementService : IDecorPlacementService
        {
            public event Action PlacementChanged;

            public IReadOnlyList<DecorPlacementEntry> GetAllPlacements() => Array.Empty<DecorPlacementEntry>();
            public string GetDecorInSlot(string slotId) => null;
            public IReadOnlyList<string> GetActiveDecorIds() => Array.Empty<string>();
            public UniTask<DecorPlacementResult> PlaceAsync(string decorId, string slotId, CancellationToken ct)
                => UniTask.FromResult(DecorPlacementResult.Success);
            public UniTask<DecorPlacementResult> ReplaceAsync(string decorId, string slotId, CancellationToken ct)
                => UniTask.FromResult(DecorPlacementResult.Success);
            public UniTask UnplaceAsync(string slotId, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask ClearAllAsync(CancellationToken ct) => UniTask.CompletedTask;

            public void RaiseChanged() => PlacementChanged?.Invoke();
        }
    }
}
