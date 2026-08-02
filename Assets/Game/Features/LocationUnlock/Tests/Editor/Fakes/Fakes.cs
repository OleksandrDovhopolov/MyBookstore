using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Conditions.API;
using Game.Conditions.Services;
using Game.Configs;
using Game.Inventory.API;
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
        public int GetSold(BookGenre genre, string locationId) => 0;
        public int GetSoldOnDay(int day) => 0;
        public int GetSoldOnDay(int day, BookGenre genre) => 0;
        public int GetMaxSoldInSingleDay(BookGenre genre) => 0;
        public int GetExcellentPicks(BookGenre genre) => 0;
        public void RecordSold(string bookId) { }
        public void RecordSold(string bookId, in SaleContext ctx) { }
        public void RecordActivePick(string bookId, in SaleContext ctx) { }
        public event Action<SalesStatsChange> Changed;

        public void RaiseChanged() => Changed?.Invoke(new SalesStatsChange(BookGenre.Crime, 0, 0, "test"));
    }

    /// <summary>Condition whose met-state can be flipped to drive Locked → Unlocked transitions.</summary>
    public sealed class FakeInventoryService : IInventoryService
    {
        private readonly Dictionary<string, (string CategoryId, int Count)> _items = new(StringComparer.Ordinal);

        public List<(string itemId, int amount)> RemoveCalls { get; } = new();

        public event Action<InventoryChangeEvent> Changed;

        public FakeInventoryService Seed(string itemId, string categoryId, int count)
        {
            _items[itemId] = (categoryId, count);
            return this;
        }

        public IReadOnlyList<InventoryItem> GetAll()
        {
            var result = new List<InventoryItem>();
            foreach (var pair in _items)
                result.Add(new InventoryItem(pair.Key, pair.Value.CategoryId, pair.Value.Count));
            return result;
        }

        public IReadOnlyList<InventoryItem> GetByCategory(string categoryId)
        {
            var result = new List<InventoryItem>();
            foreach (var pair in _items)
                if (string.Equals(pair.Value.CategoryId, categoryId, StringComparison.Ordinal))
                    result.Add(new InventoryItem(pair.Key, pair.Value.CategoryId, pair.Value.Count));
            return result;
        }

        public bool Has(string itemId) => GetCount(itemId) > 0;

        public int GetCount(string itemId)
            => !string.IsNullOrEmpty(itemId) && _items.TryGetValue(itemId, out var entry) ? entry.Count : 0;

        public UniTask AddAsync(string itemId, string categoryId, int amount, CancellationToken ct)
        {
            var current = GetCount(itemId);
            _items[itemId] = (categoryId, current + amount);
            Changed?.Invoke(new InventoryChangeEvent(categoryId, itemId, InventoryChangeKind.Added, current + amount));
            return UniTask.CompletedTask;
        }

        public UniTask AddBatchAsync(IEnumerable<InventoryItem> items, CancellationToken ct)
        {
            if (items != null)
                foreach (var item in items)
                    AddAsync(item.ItemId, item.CategoryId, item.Count, ct).GetAwaiter().GetResult();
            return UniTask.CompletedTask;
        }

        public UniTask<bool> RemoveAsync(string itemId, int amount, CancellationToken ct)
        {
            RemoveCalls.Add((itemId, amount));
            if (string.IsNullOrEmpty(itemId) || amount <= 0) return UniTask.FromResult(false);
            if (!_items.TryGetValue(itemId, out var entry) || entry.Count < amount) return UniTask.FromResult(false);

            var next = entry.Count - amount;
            if (next <= 0) _items.Remove(itemId);
            else _items[itemId] = (entry.CategoryId, next);
            Changed?.Invoke(new InventoryChangeEvent(entry.CategoryId, itemId, InventoryChangeKind.Updated, next));
            return UniTask.FromResult(true);
        }
    }

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
