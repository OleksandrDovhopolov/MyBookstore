using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Quest.API;
using Game.Rewards.API;
using NUnit.Framework;
using Save;

namespace Game.Bootstrap.Tests.Editor
{
    public sealed class QuestRewardBridgeTests
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

        [Test]
        public void Constructor_SelfRegistersAsSaveHook()
        {
            var save = new FakeSaveService();
            var bridge = new QuestRewardBridge(save, new FakeConfigs(), new FakeQuests(), new FakeRewards());

            CollectionAssert.Contains(save.RegisteredHooks, bridge);
        }

        [Test]
        public void EmptyRewards_DoNotCallGrant()
        {
            var rewards = new FakeRewards();
            var quests = new FakeQuests().Set("q1", QuestState.Awarded);
            var bridge = new QuestRewardBridge(new FakeSaveService(), new FakeConfigs(Quest("q1")), quests, rewards);

            bridge.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            bridge.BeforeSaveAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(0, rewards.Calls.Count);
        }

        [Test]
        public void AwardedQuest_GrantsInventoryRewardOnce()
        {
            var save = new FakeSaveService();
            var rewards = new FakeRewards();
            var quests = new FakeQuests().Set("q1", QuestState.Awarded);
            var configs = new FakeConfigs(Quest("q1", InventoryReward("fuel_canister", "consumable", 2)));
            var bridge = new QuestRewardBridge(save, configs, quests, rewards);

            bridge.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            bridge.BeforeSaveAsync(CancellationToken.None).GetAwaiter().GetResult();
            bridge.BeforeSaveAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(1, rewards.Calls.Count);
            Assert.AreEqual("quest:q1", rewards.Calls[0].Source);
            Assert.AreEqual("fuel_canister", rewards.Calls[0].Spec.Items[0].Id);
            Assert.AreEqual("consumable", rewards.Calls[0].Spec.Items[0].Category);
            Assert.AreEqual(2, rewards.Calls[0].Spec.Items[0].Amount);
            Assert.AreEqual(RewardKind.InventoryItem, rewards.Calls[0].Spec.Items[0].Kind);
        }

        [Test]
        public void SavedLedger_PreventsGrantAfterReload()
        {
            var save = new FakeSaveService();
            var quests = new FakeQuests().Set("q1", QuestState.Awarded);
            var configs = new FakeConfigs(Quest("q1", InventoryReward("fuel_canister", "consumable", 2)));
            var firstRewards = new FakeRewards();
            var firstBridge = new QuestRewardBridge(save, configs, quests, firstRewards);

            firstBridge.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            firstBridge.BeforeSaveAsync(CancellationToken.None).GetAwaiter().GetResult();

            var secondRewards = new FakeRewards();
            var secondBridge = new QuestRewardBridge(save, configs, quests, secondRewards);
            secondBridge.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            secondBridge.BeforeSaveAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(1, firstRewards.Calls.Count);
            Assert.AreEqual(0, secondRewards.Calls.Count);
        }

        [Test]
        public void UnknownKind_IsSkipped_ButValidRewardsGrant()
        {
            var rewards = new FakeRewards();
            var quests = new FakeQuests().Set("q1", QuestState.Awarded);
            var configs = new FakeConfigs(Quest("q1",
                new QuestRewardConfig { Kind = "Nope", Id = "bad", Amount = 1 },
                InventoryReward("fuel_canister", "consumable", 2)));
            var bridge = new QuestRewardBridge(new FakeSaveService(), configs, quests, rewards);

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex("unknown reward kind"));

            bridge.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            bridge.BeforeSaveAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(1, rewards.Calls.Count);
            Assert.AreEqual(1, rewards.Calls[0].Spec.Items.Count);
            Assert.AreEqual("fuel_canister", rewards.Calls[0].Spec.Items[0].Id);
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

            public T Get<T>(string id) where T : class, IConfig => null;
            public bool TryGet<T>(string id, out T config) where T : class, IConfig { config = null; return false; }
            public UniTask<T> GetAsync<T>(string id) where T : class, IConfig => UniTask.FromResult<T>(null);
            public bool IsExists<T>(string id) where T : class, IConfig => false;
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

            public UniTask<RewardGrantResult> GrantAsync(RewardSpec spec, string source, CancellationToken ct)
            {
                Calls.Add((spec, source));
                return UniTask.FromResult(RewardGrantResult.Ok(spec));
            }
        }
    }
}
