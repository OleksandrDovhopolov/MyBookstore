using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Conditions.API;
using Game.Conditions.Services;
using Game.Configs;
using Game.Quest.API;
using Game.Quest.Services.Persistence;
using Game.SalesStats.API;
using Game.Configs.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Save;

namespace Game.Quest.Tests.Editor.Fakes
{
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

    /// <summary>Condition whose met-state can be flipped by tests.</summary>
    public sealed class MutableCondition : ICondition
    {
        public bool Met;
        public MutableCondition(bool met) => Met = met;
        public ConditionResult Evaluate() => ConditionResult.Leaf(Met ? 1 : 0, 1, "test");
    }

    /// <summary>
    /// Maps a node by its "tag" to a registered <see cref="MutableCondition"/>; null/empty node → always-met
    /// (the real parser's contract). Lets lifecycle tests drive conditions without real factories.
    /// </summary>
    public sealed class FakeConditionParser : IConditionParser
    {
        public readonly Dictionary<string, MutableCondition> ByTag = new(StringComparer.Ordinal);

        public MutableCondition Register(string tag, bool met)
        {
            var condition = new MutableCondition(met);
            ByTag[tag] = condition;
            return condition;
        }

        public ICondition Parse(JObject node)
        {
            if (node == null || !node.HasValues) return AlwaysMetCondition.Instance;
            var tag = node.Value<string>("tag");
            if (tag != null && ByTag.TryGetValue(tag, out var condition)) return condition;
            return new NeverMetCondition("unmapped");
        }
    }

    /// <summary>In-memory quest repository; clones on load/save to mimic serialization (decoupled refs).</summary>
    public sealed class FakeQuestsRepository : IQuestsRepository
    {
        public SavedQuests Stored = new();
        public int SaveCallCount { get; private set; }

        public UniTask<SavedQuests> LoadAsync(CancellationToken ct) => UniTask.FromResult(Clone(Stored));

        public UniTask SaveAsync(SavedQuests state, CancellationToken ct)
        {
            Stored = Clone(state);
            SaveCallCount++;
            return UniTask.CompletedTask;
        }

        private static SavedQuests Clone(SavedQuests s)
        {
            var dto = new SavedQuests
            {
                Awarded = new List<string>(),
                Failed = new List<string>(),
                Active = new Dictionary<string, SavedQuest>(StringComparer.Ordinal)
            };
            if (s?.Awarded != null) dto.Awarded.AddRange(s.Awarded);
            if (s?.Failed != null) dto.Failed.AddRange(s.Failed);
            if (s?.Active != null)
                foreach (var kv in s.Active)
                {
                    var tasks = new Dictionary<int, QuestTaskState>();
                    if (kv.Value?.Tasks != null)
                        foreach (var t in kv.Value.Tasks) tasks[t.Key] = t.Value;

                    Dictionary<int, SalesStatsStateDto> baseline = null;
                    if (kv.Value?.TaskBaseline != null)
                    {
                        baseline = new Dictionary<int, SalesStatsStateDto>();
                        foreach (var b in kv.Value.TaskBaseline) baseline[b.Key] = b.Value;
                    }

                    dto.Active[kv.Key] = new SavedQuest
                    {
                        State = kv.Value?.State ?? QuestState.Active,
                        Tasks = tasks,
                        TaskBaseline = baseline
                    };
                }
            return dto;
        }
    }

    public sealed class FakeSalesStatsService : ISalesStatsService
    {
        public int GetSold(BookGenre genre) => 0;
        public int TotalSold => 0;
        public int GetSold(BookGenre genre, string locationId) => 0;
        public int GetSoldOnDay(int day) => 0;
        public int GetSoldOnDay(int day, BookGenre genre) => 0;
        public int GetMaxSoldInSingleDay(BookGenre genre) => 0;
        public void RecordSold(string bookId) { }
        public void RecordSold(string bookId, in SaleContext ctx) { }
        public event Action<SalesStatsChange> Changed;

        public void RaiseChanged() => Changed?.Invoke(new SalesStatsChange(BookGenre.Crime, 0, 0, "test"));
    }
}
