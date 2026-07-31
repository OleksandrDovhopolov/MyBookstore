using System;
using System.Collections.Generic;
using System.Threading;
using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.DayCycle.Day;
using Game.Inventory.API;
using Game.Quest.API;
using Game.Rewards.API;
using Game.Resources.API;
using Game.SalesStats.API;
using NUnit.Framework;
using Save;
using UnityEngine;
using UnityEngine.TestTools;

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

        [Test]
        public void CommitAsync_GrantsDayCompletionRewards_Once()
        {
            var save = new RecordingSaveService();
            var delivered = new RecordingDeliveredDialogues(() => save.BlockDepth > 0);
            var dayProgress = new FakeDayProgress { Current = { CurrentDay = 1 } };
            var inventory = new FakeInventoryService();
            var service = CreateService(
                save,
                dayProgress,
                delivered,
                inventory: inventory,
                configs: ConfigsWithDayRewards(Reward("postcard", InventoryCategories.Consumable, 1)));
            var result = new SalesDayResult { Day = 1, LocationId = "loc" };

            service.CommitAsync(result, CancellationToken.None).GetAwaiter().GetResult();
            service.CommitAsync(result, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(1, inventory.Added.Count);
            Assert.AreEqual("postcard", inventory.Added[0].ItemId);
            Assert.AreEqual(InventoryCategories.Consumable, inventory.Added[0].CategoryId);
            Assert.AreEqual(1, inventory.Added[0].Count);
        }

        [Test]
        public void CommitAsync_AlreadyCompletedDay_DoesNotGrantDayRewards()
        {
            var save = new RecordingSaveService();
            var delivered = new RecordingDeliveredDialogues(() => save.BlockDepth > 0);
            var dayProgress = new FakeDayProgress { Current = { CurrentDay = 1 } };
            dayProgress.Current.CompletedDays.Add(1);
            var inventory = new FakeInventoryService();
            var service = CreateService(
                save,
                dayProgress,
                delivered,
                inventory: inventory,
                configs: ConfigsWithDayRewards(Reward("postcard", InventoryCategories.Consumable, 1)));

            service.CommitAsync(new SalesDayResult { Day = 1, LocationId = "loc" }, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.AreEqual(0, inventory.Added.Count);
        }

        [Test]
        public void CommitAsync_WithoutConfigs_DoesNotThrow()
        {
            var save = new RecordingSaveService();
            var delivered = new RecordingDeliveredDialogues(() => save.BlockDepth > 0);
            var dayProgress = new FakeDayProgress { Current = { CurrentDay = 1 } };
            var service = CreateService(save, dayProgress, delivered);

            Assert.DoesNotThrow(() => service
                .CommitAsync(new SalesDayResult { Day = 1, LocationId = "loc" }, CancellationToken.None)
                .GetAwaiter().GetResult());
        }

        [Test]
        public void CommitAsync_IgnoresInvalidDayRewardEntries()
        {
            var save = new RecordingSaveService();
            var delivered = new RecordingDeliveredDialogues(() => save.BlockDepth > 0);
            var dayProgress = new FakeDayProgress { Current = { CurrentDay = 1 } };
            var inventory = new FakeInventoryService();
            var service = CreateService(
                save,
                dayProgress,
                delivered,
                inventory: inventory,
                configs: ConfigsWithDayRewards(
                    Reward(null, InventoryCategories.Consumable, 1),
                    Reward("empty_amount", InventoryCategories.Consumable, 0),
                    new RewardItemData { Id = ResourceIds.Gold, Amount = 10, Kind = RewardKind.Resource },
                    Reward("postcard", InventoryCategories.Consumable, 1)));

            LogAssert.Expect(
                LogType.Error,
                "[Sales.Commit] day-completion reward 'gold' is Resource; resources are not granted through this channel.");

            service.CommitAsync(new SalesDayResult { Day = 1, LocationId = "loc" }, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.AreEqual(1, inventory.Added.Count);
            Assert.AreEqual("postcard", inventory.Added[0].ItemId);
        }

        [Test]
        public void CommitAsync_RecordsOnlyExcellentRecommendationsAsActivePicks()
        {
            var save = new RecordingSaveService();
            var delivered = new RecordingDeliveredDialogues(() => save.BlockDepth > 0);
            var dayProgress = new FakeDayProgress { Current = { CurrentDay = 1 } };
            var salesStats = new FakeSalesStatsRecorder();
            var service = CreateService(save, dayProgress, delivered, salesStats);
            var result = new SalesDayResult { Day = 1, LocationId = "loc" };
            result.Recommendations.Add(Recommendation("book_fact", RecommendationTier.Excellent));
            result.Recommendations.Add(Recommendation("book_crime", RecommendationTier.Failed));
            result.Recommendations.Add(RecommendationResult.Skipped("req_skip"));
            result.Recommendations.Add(Recommendation(null, RecommendationTier.Excellent));

            service.CommitAsync(result, CancellationToken.None).GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { "book_fact" }, salesStats.ActivePicks);
        }

        [Test]
        public void CommitAsync_AlreadyCompletedDay_DoesNotRecordActivePicks()
        {
            var save = new RecordingSaveService();
            var delivered = new RecordingDeliveredDialogues(() => save.BlockDepth > 0);
            var dayProgress = new FakeDayProgress { Current = { CurrentDay = 1 } };
            dayProgress.Current.CompletedDays.Add(1);
            var salesStats = new FakeSalesStatsRecorder();
            var service = CreateService(save, dayProgress, delivered, salesStats);
            var result = new SalesDayResult { Day = 1, LocationId = "loc" };
            result.Recommendations.Add(Recommendation("book_fact", RecommendationTier.Excellent));

            service.CommitAsync(result, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(0, salesStats.ActivePicks.Count);
        }

        private static RecommendationResult Recommendation(string bookId, RecommendationTier tier)
            => new("req", bookId, tier, default, RecommendationReason.Empty, 0);

        private static FakeConfigsService ConfigsWithDayRewards(params RewardItemData[] rewards)
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[]
            {
                new EconomyConfig
                {
                    Id = EconomyConfig.SingletonId,
                    DayCompletionRewards = rewards
                }
            });
            return configs;
        }

        private static RewardItemData Reward(string id, string category, int amount) =>
            new()
            {
                Id = id,
                Category = category,
                Amount = amount,
                Kind = RewardKind.InventoryItem
            };

        private static SalesDayCommitService CreateService(
            RecordingSaveService save,
            IDayProgressService dayProgress,
            IDeliveredDialoguesService delivered,
            FakeSalesStatsRecorder salesStats = null,
            FakeInventoryService inventory = null,
            IConfigsService configs = null)
            => new(
                save,
                new FakeResourcesService(),
                inventory ?? new FakeInventoryService(),
                new FakeShelfStateService(),
                salesStats ?? new FakeSalesStatsRecorder(),
                dayProgress,
                new FakeQuestReevaluationGate(),
                delivered,
                configs);

        private sealed class RecordingDeliveredDialogues : IDeliveredDialoguesService
        {
            private readonly Func<bool> _isAutosaveBlocked;

            public RecordingDeliveredDialogues(Func<bool> isAutosaveBlocked)
                => _isAutosaveBlocked = isAutosaveBlocked;

            public int CommitCalls { get; private set; }
            public bool WasCommittedInsideAutosaveBlock { get; private set; }
            public event Action Changed;
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
            public readonly List<InventoryItem> Added = new();

            public event Action<InventoryChangeEvent> Changed { add { } remove { } }
            public IReadOnlyList<InventoryItem> GetAll() => Array.Empty<InventoryItem>();
            public IReadOnlyList<InventoryItem> GetByCategory(string categoryId) => Array.Empty<InventoryItem>();
            public bool Has(string itemId) => false;
            public int GetCount(string itemId) => 0;

            public UniTask AddAsync(string itemId, string categoryId, int amount, CancellationToken ct)
            {
                Added.Add(new InventoryItem(itemId, categoryId, amount));
                return UniTask.CompletedTask;
            }

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
            public readonly List<string> Sold = new();
            public readonly List<string> ActivePicks = new();

            public void RecordSold(string bookId) => Sold.Add(bookId);
            public void RecordSold(string bookId, in SaleContext ctx) => Sold.Add(bookId);
            public void RecordActivePick(string bookId, in SaleContext ctx) => ActivePicks.Add(bookId);
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
