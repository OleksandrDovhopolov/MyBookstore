using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Characters.Services.Persistence;
using Game.Configs;
using Game.Configs.Models;
using Game.Quest.API;
using Newtonsoft.Json;
using Save;

namespace Game.Characters.Tests.Editor.Fakes
{
    /// <summary>In-memory <see cref="ISaveService"/> with JSON round-trip (mirrors Quest's FakeSaveService).</summary>
    public sealed class FakeSaveService : ISaveService
    {
        private readonly Dictionary<string, string> _store = new();
        public List<ISaveHook> RegisteredHooks { get; } = new();

        public UniTask<T> GetModuleAsync<T>(string moduleKey, CancellationToken ct) where T : class
        {
            if (_store.TryGetValue(moduleKey, out var json) && !string.IsNullOrEmpty(json))
                return UniTask.FromResult(JsonConvert.DeserializeObject<T>(json));
            return UniTask.FromResult<T>(null);
        }

        public UniTask UpdateModuleAsync<T>(string moduleKey, T value, int schemaVersion, CancellationToken ct)
        {
            _store[moduleKey] = JsonConvert.SerializeObject(value, Formatting.None);
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

    /// <summary>In-memory <see cref="IConfigsService"/>; only the methods the service uses are meaningful.</summary>
    public sealed class FakeConfigsService : IConfigsService
    {
        private readonly Dictionary<string, IConfig> _byId = new(StringComparer.OrdinalIgnoreCase);

        public FakeConfigsService Add(IConfig config)
        {
            if (config != null && !string.IsNullOrEmpty(config.Id)) _byId[config.Id] = config;
            return this;
        }

        public T Get<T>(string id) where T : class, IConfig
            => id != null && _byId.TryGetValue(id, out var cfg) ? cfg as T : null;

        public bool TryGet<T>(string id, out T config) where T : class, IConfig
        {
            config = Get<T>(id);
            return config != null;
        }

        public UniTask<T> GetAsync<T>(string id) where T : class, IConfig => UniTask.FromResult(Get<T>(id));
        public bool IsExists<T>(string id) where T : class, IConfig => Get<T>(id) != null;

        public IReadOnlyList<T> GetAll<T>() where T : class, IConfig
        {
            var list = new List<T>();
            foreach (var cfg in _byId.Values)
                if (cfg is T typed) list.Add(typed);
            return list;
        }

        public UniTask WarmupAsync(CancellationToken ct) => UniTask.CompletedTask;
    }

    /// <summary>In-memory characters repository (no serialization). For service-level tests.</summary>
    public sealed class FakeCharactersRepository : ICharactersRepository
    {
        public SavedCharacters Stored = new();

        public UniTask<SavedCharacters> LoadAsync(CancellationToken ct) => UniTask.FromResult(Stored);

        public UniTask SaveAsync(SavedCharacters state, CancellationToken ct)
        {
            Stored = state;
            return UniTask.CompletedTask;
        }
    }

    /// <summary>
    /// Minimal <see cref="IQuestsService"/> exposing only what <c>CharacterModelFactory</c> reads:
    /// <see cref="GetQuestState"/> and <see cref="GetChain"/>. Everything else is a benign stub.
    /// </summary>
    public sealed class FakeQuestsService : IQuestsService
    {
        public readonly Dictionary<string, QuestState> States = new(StringComparer.Ordinal);
        public readonly Dictionary<string, IQuestChain> Chains = new(StringComparer.Ordinal);

        public FakeQuestsService SetState(string questId, QuestState state)
        {
            States[questId] = state;
            return this;
        }

        public FakeQuestsService AddChain(string chainId, params IQuest[] quests)
        {
            Chains[chainId] = new FakeQuestChain(chainId, quests);
            return this;
        }

        public QuestState GetQuestState(string questId)
            => questId != null && States.TryGetValue(questId, out var s) ? s : QuestState.Pending;

        public IQuestChain GetChain(string chainId)
            => chainId != null && Chains.TryGetValue(chainId, out var c) ? c : null;

        // ----- Unused by the read-side factory: benign stubs -----
        public IQuest TryGetQuest(string questId) => null;
        public QuestConfig GetQuestConfig(string questId) => null;
        public IEnumerable<IQuest> GetActiveQuests() => Array.Empty<IQuest>();
        public IQuestChain GetChainByQuestId(string questId) => null;
        public UniTask<bool> TryActivateAsync(string questId, CancellationToken ct) => UniTask.FromResult(false);
        public UniTask<bool> TryAwardAsync(string questId, CancellationToken ct) => UniTask.FromResult(false);
        public UniTask<bool> TryFailAsync(string questId, CancellationToken ct) => UniTask.FromResult(false);

#pragma warning disable 67
        public event Action<IQuest> QuestStarted;
        public event Action<IQuest> QuestCompleted;
        public event Action<IQuest> QuestAwarded;
        public event Action<IQuest> QuestFailed;
        public event Action<IQuestTask> TaskCompleted;
        public event Action<IQuestTask> TaskProgressChanged;
#pragma warning restore 67
    }

    public sealed class FakeQuest : IQuest
    {
        public FakeQuest(string id, QuestState state, string chainId = null)
        {
            Id = id;
            State = state;
            ChainId = chainId;
        }

        public string Id { get; }
        public QuestType Type => default;
        public QuestState State { get; }
        public string ChainId { get; }
        public string CharacterId => null;
        public QuestConfig Config => null;
        public IReadOnlyList<IQuestTask> Tasks => Array.Empty<IQuestTask>();
        public IQuestTask GetTask(int id) => null;
    }

    /// <summary>Computes CurrentQuest/FinalQuest exactly like the real QuestChain.</summary>
    public sealed class FakeQuestChain : IQuestChain
    {
        public FakeQuestChain(string id, IReadOnlyList<IQuest> quests)
        {
            Id = id;
            Quests = quests ?? Array.Empty<IQuest>();
        }

        public string Id { get; }
        public IReadOnlyList<IQuest> Quests { get; }

        public IQuest FinalQuest => Quests.Count > 0 ? Quests[Quests.Count - 1] : null;

        public IQuest CurrentQuest
        {
            get
            {
                IQuest firstPending = null;
                for (var i = 0; i < Quests.Count; i++)
                {
                    var q = Quests[i];
                    if (q.State == QuestState.Active) return q;
                    if (firstPending == null && q.State == QuestState.Pending) firstPending = q;
                }
                return firstPending ?? FinalQuest;
            }
        }
    }
}
