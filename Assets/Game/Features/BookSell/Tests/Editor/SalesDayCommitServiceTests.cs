using System;
using System.Collections.Generic;
using System.Threading;
using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Services;
using Cysharp.Threading.Tasks;
using Game.DayCycle.Day;
using Game.Inventory.API;
using Game.Quest.API;
using Game.Resources.API;
using Game.SalesStats.API;
using NUnit.Framework;
using Save;

namespace Book.Sell.Tests.Editor
{
    public sealed class SalesDayCommitServiceTests
    {
        [Test]
        public void CommitAsync_CommitsDeliveredInsideAutosaveBlock_AndForceSavesOnce()
        {
            var save = new RecordingSaveService();
            var delivered = new RecordingDeliveredDialogues(() => save.BlockDepth > 0);
            var dayProgress = new FakeDayProgress { Current = { CurrentDay = 1 } };
            var service = CreateService(save, dayProgress, delivered);

            service.CommitAsync(new SalesDayResult { Day = 1, LocationId = "loc" }, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.AreEqual(1, delivered.CommitCalls);
            Assert.IsTrue(delivered.WasCommittedInsideAutosaveBlock);
            Assert.AreEqual(1, save.ForceWithSyncSaveCalls);
            CollectionAssert.Contains(dayProgress.Current.CompletedDays, 1);
        }

        [Test]
        public void CommitAsync_AlreadyCompletedDay_DoesNotCommitDeliveredOrSave()
        {
            var save = new RecordingSaveService();
            var delivered = new RecordingDeliveredDialogues(() => save.BlockDepth > 0);
            var dayProgress = new FakeDayProgress { Current = { CurrentDay = 1 } };
            dayProgress.Current.CompletedDays.Add(1);
            var service = CreateService(save, dayProgress, delivered);

            service.CommitAsync(new SalesDayResult { Day = 1, LocationId = "loc" }, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.AreEqual(0, delivered.CommitCalls);
            Assert.AreEqual(0, save.ForceWithSyncSaveCalls);
        }

        private static SalesDayCommitService CreateService(
            RecordingSaveService save,
            IDayProgressService dayProgress,
            IDeliveredDialoguesService delivered)
            => new(
                save,
                new FakeResourcesService(),
                new FakeInventoryService(),
                new FakeShelfStateService(),
                new FakeSalesStatsRecorder(),
                dayProgress,
                new FakeQuestReevaluationGate(),
                delivered);

        private sealed class RecordingDeliveredDialogues : IDeliveredDialoguesService
        {
            private readonly Func<bool> _isAutosaveBlocked;

            public RecordingDeliveredDialogues(Func<bool> isAutosaveBlocked)
                => _isAutosaveBlocked = isAutosaveBlocked;

            public int CommitCalls { get; private set; }
            public bool WasCommittedInsideAutosaveBlock { get; private set; }
            public bool IsDelivered(string dialogueId) => false;
            public UniTask MarkDeliveredAsync(string dialogueId, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask MarkDeliveredDeferredAsync(string dialogueId, CancellationToken ct) => UniTask.CompletedTask;
            public void DiscardDeferred() { }

            public UniTask CommitAsync(CancellationToken ct)
            {
                CommitCalls++;
                WasCommittedInsideAutosaveBlock |= _isAutosaveBlocked();
                return UniTask.CompletedTask;
            }
        }

        private sealed class RecordingSaveService : ISaveService
        {
            private readonly Dictionary<string, object> _modules = new();

            public int BlockDepth { get; private set; }
            public int ForceWithSyncSaveCalls { get; private set; }

            public UniTask LoadAsync(CancellationToken ct) => UniTask.CompletedTask;

            public UniTask SaveAsync(CancellationToken ct, SaveMode mode = SaveMode.Regular)
            {
                if (mode == SaveMode.ForceWithSync)
                    ForceWithSyncSaveCalls++;

                return UniTask.CompletedTask;
            }

            public UniTask<T> GetModuleAsync<T>(string moduleKey, CancellationToken ct) where T : class
                => UniTask.FromResult(_modules.TryGetValue(moduleKey, out var value) ? value as T : null);

            public UniTask UpdateModuleAsync<T>(string moduleKey, T value, int schemaVersion, CancellationToken ct)
            {
                _modules[moduleKey] = value;
                return UniTask.CompletedTask;
            }

            public void MarkDirty() { }
            public void RegisterHook(ISaveHook hook) { }

            public IDisposable BlockAutosave()
            {
                BlockDepth++;
                return new Lease(this);
            }

            public void Dispose() { }

            private sealed class Lease : IDisposable
            {
                private RecordingSaveService _owner;
                public Lease(RecordingSaveService owner) => _owner = owner;

                public void Dispose()
                {
                    if (_owner == null) return;
                    _owner.BlockDepth--;
                    _owner = null;
                }
            }
        }

        private sealed class FakeDayProgress : IDayProgressService
        {
            public event Action<DayProgressState> PhaseChanged { add { } remove { } }
            public DayProgressState Current { get; set; } = new();
            public UniTask<DayProgressState> LoadAsync(CancellationToken ct) => UniTask.FromResult(Current);
            public UniTask SetPhaseAsync(DayPhase phase, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask MarkCurrentDayCompletedAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask AdvanceToNextDayAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SaveAsync(CancellationToken ct) => UniTask.CompletedTask;
        }

        private sealed class FakeResourcesService : IResourcesService
        {
            public event Action<ResourceChangeEvent> Changed { add { } remove { } }
            public IReadOnlyDictionary<string, int> GetAll() => new Dictionary<string, int>();
            public int GetAmount(string resourceId) => 0;
            public bool Has(string resourceId, int amount) => false;
            public UniTask AddAsync(string resourceId, int amount, string reason, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask<bool> RemoveAsync(string resourceId, int amount, string reason, CancellationToken ct)
                => UniTask.FromResult(false);
        }

        private sealed class FakeInventoryService : IInventoryService
        {
            public event Action<InventoryChangeEvent> Changed { add { } remove { } }
            public IReadOnlyList<InventoryItem> GetAll() => Array.Empty<InventoryItem>();
            public IReadOnlyList<InventoryItem> GetByCategory(string categoryId) => Array.Empty<InventoryItem>();
            public bool Has(string itemId) => false;
            public int GetCount(string itemId) => 0;
            public UniTask AddAsync(string itemId, string categoryId, int amount, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask AddBatchAsync(IEnumerable<InventoryItem> items, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask<bool> RemoveAsync(string itemId, int amount, CancellationToken ct) => UniTask.FromResult(false);
        }

        private sealed class FakeShelfStateService : ISalesShelfStateService
        {
            public IReadOnlyList<string> ShelfBookIds => Array.Empty<string>();
            public SalesShelfState CurrentState { get; } = new();
            public bool IsSold(string bookId) => false;
            public UniTask SetShelfAsync(IReadOnlyList<string> bookIds, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask MarkSoldAsync(string bookId, CancellationToken ct) => UniTask.CompletedTask;
        }

        private sealed class FakeSalesStatsRecorder : ISalesStatsRecorder
        {
            public void RecordSold(string bookId) { }
            public void RecordSold(string bookId, in SaleContext ctx) { }
        }

        private sealed class FakeQuestReevaluationGate : IQuestReevaluationGate
        {
            public IDisposable SuspendReevaluation() => new Lease();
            public void RequestReevaluation() { }

            private sealed class Lease : IDisposable
            {
                public void Dispose() { }
            }
        }
    }
}
