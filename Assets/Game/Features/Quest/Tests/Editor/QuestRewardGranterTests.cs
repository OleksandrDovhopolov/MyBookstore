using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using Game.Quest.API;
using Game.Quest.Services;
using Game.Rewards.API;
using NUnit.Framework;
using Save;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Quest.Tests.Editor
{
    public sealed class QuestRewardGranterTests
    {
        private static QuestConfig Quest(string id, params QuestRewardConfig[] rewards)
            => new()
            {
                Id = id,
                Type = "story",
                Tasks = new[] { new QuestTaskConfig { Id = 1 } },
                Rewards = rewards
            };

        private static QuestRewardConfig InventoryReward(string id, string category, int amount)
            => new() { Kind = nameof(RewardKind.InventoryItem), Id = id, Category = category, Amount = amount };

        private static QuestCostConfig QuestCost(string itemId, int amount)
            => new() { ItemId = itemId, Amount = amount };

        [Test]
        public void Constructor_SelfRegistersAsSaveHook()
        {
            var save = new FakeSaveService();
            var granter = new QuestRewardGranter(
                save,
                new FakeConfigs(),
                new FakeQuests(),
                new FakeRewards(),
                new FakeInventoryService());

            CollectionAssert.Contains(save.RegisteredHooks, granter);
        }

        [Test]
        public void TryGrantAsync_NotAwarded_DoesNotGrant()
        {
            var rewards = new FakeRewards();
            var quests = new FakeQuests().Set("q1", QuestState.ReadyToAward);
            var inventory = new FakeInventoryService().Set("postcard", InventoryCategories.Consumable, 10);
            var granter = new QuestRewardGranter(new FakeSaveService(), new FakeConfigs(Quest("q1")), quests, rewards, inventory);

            granter.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            var result = granter.TryGrantAsync("q1", CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(result.Success);
            Assert.AreEqual("not_awarded", result.FailureReason);
            Assert.AreEqual(0, rewards.Calls.Count);
            Assert.AreEqual(0, inventory.RemoveCalls);
        }

        [Test]
        public void TryGrantAsync_GrantsInventoryRewardOnce()
        {
            var save = new FakeSaveService();
            var rewards = new FakeRewards();
            var quests = new FakeQuests().Set("q1", QuestState.Awarded);
            var configs = new FakeConfigs(Quest("q1", InventoryReward("fuel_canister", InventoryCategories.Consumable, 2)));
            var inventory = new FakeInventoryService();
            var granter = new QuestRewardGranter(save, configs, quests, rewards, inventory);

            granter.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            var first = granter.TryGrantAsync("q1", CancellationToken.None).GetAwaiter().GetResult();
            var second = granter.TryGrantAsync("q1", CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(first.Success);
            Assert.IsTrue(second.AlreadyGranted);
            Assert.AreEqual(1, rewards.Calls.Count);
            Assert.AreEqual("quest:q1", rewards.Calls[0].Source);
            Assert.AreEqual("fuel_canister", rewards.Calls[0].Spec.Items[0].Id);
            Assert.AreEqual(InventoryCategories.Consumable, rewards.Calls[0].Spec.Items[0].Category);
            Assert.AreEqual(2, rewards.Calls[0].Spec.Items[0].Amount);
            Assert.AreEqual(RewardKind.InventoryItem, rewards.Calls[0].Spec.Items[0].Kind);
        }

        [Test]
        public void TryGrantAsync_ConsumesQuestCosts_Once()
        {
            var save = new FakeSaveService();
            var rewards = new FakeRewards();
            var quests = new FakeQuests().Set("q1", QuestState.Awarded);
            var quest = Quest("q1", InventoryReward("captain_recommendation", InventoryCategories.QuestItem, 1));
            quest.Costs = new[] { QuestCost("postcard", 10) };
            var inventory = new FakeInventoryService().Set("postcard", InventoryCategories.Consumable, 10);
            var granter = new QuestRewardGranter(save, new FakeConfigs(quest), quests, rewards, inventory);

            granter.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            var result = granter.TryGrantAsync("q1", CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, inventory.GetCount("postcard"));
            Assert.AreEqual(1, inventory.RemoveCalls);
        }

        [Test]
        public void TryGrantAsync_SecondCall_DoesNotConsumeAgain()
        {
            var save = new FakeSaveService();
            var rewards = new FakeRewards();
            var quests = new FakeQuests().Set("q1", QuestState.Awarded);
            var quest = Quest("q1", InventoryReward("captain_recommendation", InventoryCategories.QuestItem, 1));
            quest.Costs = new[] { QuestCost("postcard", 10) };
            var inventory = new FakeInventoryService().Set("postcard", InventoryCategories.Consumable, 20);
            var granter = new QuestRewardGranter(save, new FakeConfigs(quest), quests, rewards, inventory);

            granter.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            var first = granter.TryGrantAsync("q1", CancellationToken.None).GetAwaiter().GetResult();
            var second = granter.TryGrantAsync("q1", CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(first.Success);
            Assert.IsTrue(second.AlreadyGranted);
            Assert.AreEqual(10, inventory.GetCount("postcard"));
            Assert.AreEqual(1, inventory.RemoveCalls);
        }

        [Test]
        public void TryGrantAsync_NoCosts_DoesNotTouchInventory()
        {
            var rewards = new FakeRewards();
            var quests = new FakeQuests().Set("q1", QuestState.Awarded);
            var inventory = new FakeInventoryService().Set("postcard", InventoryCategories.Consumable, 10);
            var configs = new FakeConfigs(Quest("q1", InventoryReward("fuel_canister", InventoryCategories.Consumable, 1)));
            var granter = new QuestRewardGranter(new FakeSaveService(), configs, quests, rewards, inventory);

            granter.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            var result = granter.TryGrantAsync("q1", CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.AreEqual(10, inventory.GetCount("postcard"));
            Assert.AreEqual(0, inventory.RemoveCalls);
        }

        [Test]
        public void TryGrantAsync_GrantFailure_DoesNotConsumeAndCanRetry()
        {
            var rewards = new FakeRewards { FailNext = true };
            var quests = new FakeQuests().Set("q1", QuestState.Awarded);
            var quest = Quest("q1", InventoryReward("fuel_canister", InventoryCategories.Consumable, 1));
            quest.Costs = new[] { QuestCost("postcard", 10) };
            var inventory = new FakeInventoryService().Set("postcard", InventoryCategories.Consumable, 10);
            var configs = new FakeConfigs(quest);
            var granter = new QuestRewardGranter(new FakeSaveService(), configs, quests, rewards, inventory);

            granter.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            var failed = granter.TryGrantAsync("q1", CancellationToken.None).GetAwaiter().GetResult();
            var retried = granter.TryGrantAsync("q1", CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(failed.Success);
            Assert.AreEqual("grant_failed", failed.FailureReason);
            Assert.AreEqual(0, inventory.RemoveCalls);
            Assert.IsTrue(retried.Success);
            Assert.AreEqual(0, inventory.GetCount("postcard"));
            Assert.AreEqual(1, inventory.RemoveCalls);
            Assert.AreEqual(2, rewards.Calls.Count);
        }

        [Test]
        public void TryGrantAsync_ConsumeFailure_StillMarksGranted()
        {
            var rewards = new FakeRewards();
            var quests = new FakeQuests().Set("q1", QuestState.Awarded);
            var quest = Quest("q1", InventoryReward("captain_recommendation", InventoryCategories.QuestItem, 1));
            quest.Costs = new[] { QuestCost("postcard", 10) };
            var inventory = new FakeInventoryService { FailRemove = true }
                .Set("postcard", InventoryCategories.Consumable, 10);
            var granter = new QuestRewardGranter(new FakeSaveService(), new FakeConfigs(quest), quests, rewards, inventory);

            LogAssert.Expect(LogType.Error, new Regex("failed to consume cost 'postcard' x10 for quest 'q1'"));

            granter.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            var result = granter.TryGrantAsync("q1", CancellationToken.None).GetAwaiter().GetResult();
            var second = granter.TryGrantAsync("q1", CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.IsTrue(second.AlreadyGranted);
            Assert.IsTrue(granter.IsGranted("q1"));
            Assert.AreEqual(10, inventory.GetCount("postcard"));
            Assert.AreEqual(1, inventory.RemoveCalls);
        }

        [Test]
        public void BeforeSave_SweepsAwardedQuestWithoutDoubleGrant()
        {
            var rewards = new FakeRewards();
            var quests = new FakeQuests().Set("q1", QuestState.Awarded);
            var configs = new FakeConfigs(Quest("q1", InventoryReward("fuel_canister", InventoryCategories.Consumable, 2)));
            var granter = new QuestRewardGranter(new FakeSaveService(), configs, quests, rewards, new FakeInventoryService());

            granter.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            granter.BeforeSaveAsync(CancellationToken.None).GetAwaiter().GetResult();
            granter.BeforeSaveAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(1, rewards.Calls.Count);
        }

        [Test]
        public void BeforeSave_SweepsAwardedQuestWithoutDoubleConsume()
        {
            var rewards = new FakeRewards();
            var quests = new FakeQuests().Set("q1", QuestState.Awarded);
            var quest = Quest("q1", InventoryReward("captain_recommendation", InventoryCategories.QuestItem, 1));
            quest.Costs = new[] { QuestCost("postcard", 10) };
            var inventory = new FakeInventoryService().Set("postcard", InventoryCategories.Consumable, 20);
            var granter = new QuestRewardGranter(new FakeSaveService(), new FakeConfigs(quest), quests, rewards, inventory);

            granter.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            granter.BeforeSaveAsync(CancellationToken.None).GetAwaiter().GetResult();
            granter.BeforeSaveAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(1, rewards.Calls.Count);
            Assert.AreEqual(10, inventory.GetCount("postcard"));
            Assert.AreEqual(1, inventory.RemoveCalls);
        }

        [Test]
        public void SavedLedger_PreventsGrantAfterReload()
        {
            var save = new FakeSaveService();
            var quests = new FakeQuests().Set("q1", QuestState.Awarded);
            var configs = new FakeConfigs(Quest("q1", InventoryReward("fuel_canister", InventoryCategories.Consumable, 2)));
            var firstRewards = new FakeRewards();
            var inventory = new FakeInventoryService();
            var first = new QuestRewardGranter(save, configs, quests, firstRewards, inventory);

            first.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            first.TryGrantAsync("q1", CancellationToken.None).GetAwaiter().GetResult();

            var secondRewards = new FakeRewards();
            var second = new QuestRewardGranter(save, configs, quests, secondRewards, inventory);
            second.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            var result = second.TryGrantAsync("q1", CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.AlreadyGranted);
            Assert.AreEqual(1, firstRewards.Calls.Count);
            Assert.AreEqual(0, secondRewards.Calls.Count);
        }

        private sealed class FakeSaveService : ISaveService
        {
            private readonly Dictionary<string, object> _store = new();
            public List<ISaveHook> RegisteredHooks { get; } = new();

            public UniTask<T> GetModuleAsync<T>(string moduleKey, CancellationToken ct) where T : class
                => UniTask.FromResult(_store.TryGetValue(moduleKey, out var value) ? value as T : null);

            public UniTask UpdateModuleAsync<T>(string moduleKey, T value, int schemaVersion, CancellationToken ct)
            {
                _store[moduleKey] = value;
                return UniTask.CompletedTask;
            }

            public UniTask LoadAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SaveAsync(CancellationToken ct, SaveMode mode = SaveMode.Regular) => UniTask.CompletedTask;
            public void MarkDirty() { }
            public void RegisterHook(ISaveHook hook) { if (hook != null) RegisteredHooks.Add(hook); }
            public IDisposable BlockAutosave() => new NoopLease();
            public void Dispose() { }

            private sealed class NoopLease : IDisposable { public void Dispose() { } }
        }

        private sealed class FakeConfigs : IConfigsService
        {
            private readonly List<IConfig> _configs = new();

            public FakeConfigs(params IConfig[] configs)
            {
                if (configs != null) _configs.AddRange(configs);
            }

            public T Get<T>(string id) where T : class, IConfig
            {
                foreach (var config in _configs)
                    if (config is T typed && string.Equals(typed.Id, id, StringComparison.Ordinal))
                        return typed;
                return null;
            }

            public bool TryGet<T>(string id, out T config) where T : class, IConfig
            {
                config = Get<T>(id);
                return config != null;
            }

            public UniTask<T> GetAsync<T>(string id) where T : class, IConfig => UniTask.FromResult(Get<T>(id));
            public bool IsExists<T>(string id) where T : class, IConfig => Get<T>(id) != null;

            public IReadOnlyList<T> GetAll<T>() where T : class, IConfig
            {
                var result = new List<T>();
                foreach (var config in _configs)
                    if (config is T typed) result.Add(typed);
                return result;
            }

            public UniTask WarmupAsync(CancellationToken ct) => UniTask.CompletedTask;
        }

        private sealed class FakeQuests : IQuestsService
        {
            private readonly Dictionary<string, QuestState> _states = new(StringComparer.Ordinal);

            public FakeQuests Set(string questId, QuestState state)
            {
                _states[questId] = state;
                return this;
            }

            public IQuest TryGetQuest(string questId) => null;
            public QuestConfig GetQuestConfig(string questId) => null;
            public QuestState GetQuestState(string questId)
                => questId != null && _states.TryGetValue(questId, out var state) ? state : QuestState.Pending;
            public IReadOnlyList<IQuest> GetAllQuests() => Array.Empty<IQuest>();
            public IEnumerable<IQuest> GetActiveQuests() => Array.Empty<IQuest>();
            public IQuestChain GetChain(string chainId) => null;
            public IQuestChain GetChainByQuestId(string questId) => null;
            public UniTask<bool> TryActivateAsync(string questId, CancellationToken ct) => UniTask.FromResult(false);
            public UniTask<bool> TryAwardAsync(string questId, CancellationToken ct) => UniTask.FromResult(false);
            public UniTask<bool> TryFailAsync(string questId, CancellationToken ct) => UniTask.FromResult(false);
            public event Action<IQuest> QuestStarted;
            public event Action<IQuest> QuestCompleted;
            public event Action<IQuest> QuestAwarded;
            public event Action<IQuest> QuestFailed;
            public event Action<IQuestTask> TaskCompleted;
            public event Action<IQuestTask> TaskProgressChanged;
        }

        private sealed class FakeRewards : IRewardGrantService
        {
            public List<(RewardSpec Spec, string Source)> Calls { get; } = new();
            public bool FailNext { get; set; }

            public UniTask<RewardGrantResult> GrantAsync(RewardSpec spec, string source, CancellationToken ct)
            {
                Calls.Add((spec, source));
                if (FailNext)
                {
                    FailNext = false;
                    return UniTask.FromResult(RewardGrantResult.Fail("grant_failed"));
                }

                return UniTask.FromResult(RewardGrantResult.Ok(spec));
            }
        }

        private sealed class FakeInventoryService : IInventoryService
        {
            private readonly Dictionary<string, (string Category, int Count)> _items = new(StringComparer.Ordinal);

            public int RemoveCalls { get; private set; }
            public bool FailRemove { get; set; }

            public FakeInventoryService Set(string itemId, string categoryId, int count)
            {
                _items[itemId] = (categoryId, count);
                return this;
            }

            public IReadOnlyList<InventoryItem> GetAll() => Array.Empty<InventoryItem>();
            public IReadOnlyList<InventoryItem> GetByCategory(string categoryId) => Array.Empty<InventoryItem>();
            public bool Has(string itemId) => GetCount(itemId) > 0;

            public int GetCount(string itemId)
                => itemId != null && _items.TryGetValue(itemId, out var item) ? item.Count : 0;

            public UniTask AddAsync(string itemId, string categoryId, int amount, CancellationToken ct)
            {
                var current = GetCount(itemId);
                _items[itemId] = (categoryId, current + amount);
                return UniTask.CompletedTask;
            }

            public UniTask AddBatchAsync(IEnumerable<InventoryItem> items, CancellationToken ct)
                => UniTask.CompletedTask;

            public UniTask<bool> RemoveAsync(string itemId, int amount, CancellationToken ct)
            {
                RemoveCalls++;
                if (FailRemove) return UniTask.FromResult(false);
                if (string.IsNullOrEmpty(itemId) || amount <= 0) return UniTask.FromResult(false);
                if (!_items.TryGetValue(itemId, out var item) || item.Count < amount)
                    return UniTask.FromResult(false);

                var remaining = item.Count - amount;
                if (remaining <= 0)
                    _items.Remove(itemId);
                else
                    _items[itemId] = (item.Category, remaining);

                return UniTask.FromResult(true);
            }

            public event Action<InventoryChangeEvent> Changed;
        }
    }
}
