using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Conditions.API;
using Game.Conditions.Services;
using Game.Configs;
using Game.LocationUnlock.API;
using Game.SalesStats.API;
using Game.Configs.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Save;

namespace Game.LocationUnlock.Tests.Editor.Fakes
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

    public sealed class FakeLocationUnlockRepository : ILocationUnlockRepository
    {
        public LocationUnlockStateDto Stored { get; set; } = new();
        public int SaveCallCount { get; private set; }

        public UniTask<LocationUnlockStateDto> LoadAsync(CancellationToken ct)
            => UniTask.FromResult(new LocationUnlockStateDto
            {
                UnlockedIds = Stored?.UnlockedIds != null ? new List<string>(Stored.UnlockedIds) : new List<string>()
            });

        public UniTask SaveAsync(LocationUnlockStateDto state, CancellationToken ct)
        {
            Stored = new LocationUnlockStateDto
            {
                UnlockedIds = state?.UnlockedIds != null ? new List<string>(state.UnlockedIds) : new List<string>()
            };
            SaveCallCount++;
            return UniTask.CompletedTask;
        }
    }

    public sealed class FakeSalesStatsService : ISalesStatsService
    {
        public int GetSold(BookGenre genre) => 0;
        public int TotalSold => 0;
        public void RecordSold(string bookId) { }
        public event Action<SalesStatsChange> Changed;

        public void RaiseChanged() => Changed?.Invoke(new SalesStatsChange(BookGenre.Crime, 0, 0, "test"));
    }

    /// <summary>Condition whose met-state can be flipped to drive Locked → Unlocked transitions.</summary>
    public sealed class MutableCondition : ICondition
    {
        public bool Met;
        public MutableCondition(bool met) => Met = met;
        public ConditionResult Evaluate() => ConditionResult.Leaf(Met ? 1 : 0, 1, "test");
    }

    /// <summary>
    /// Returns a registered <see cref="MutableCondition"/> keyed by the node's "tag"; a null/empty
    /// node maps to always-met (mirrors the real parser contract).
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
}
