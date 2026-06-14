using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Inventory.API;
using Save;
using UnityEngine;

namespace Game.Inventory.Services
{
    /// <summary>
    /// Default <see cref="IInventoryService"/> implementation. Acts as an <see cref="ISaveHook"/>:
    /// on AfterLoadAsync it pulls the persisted state from <see cref="IInventoryRepository"/>
    /// into in-memory dictionaries; writes go to the repository and update the cache in lockstep.
    /// </summary>
    public sealed class InventoryService : IInventoryService, ISaveHook
    {
        private const string LogPrefix = "[Inventory]";

        private readonly IInventoryRepository _repository;
        private readonly IItemCategoryRegistry _categories;

        // Unique categories: items[itemId] = categoryId.
        private readonly Dictionary<string, string> _uniques = new(StringComparer.Ordinal);

        // Stack categories: stacks[itemId] = (categoryId, count).
        private readonly Dictionary<string, (string CategoryId, int Count)> _stacks = new(StringComparer.Ordinal);

        private bool _loaded;

        public InventoryService(ISaveService save, IInventoryRepository repository, IItemCategoryRegistry categories)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _categories = categories ?? throw new ArgumentNullException(nameof(categories));

            // Self-register as a save hook so AfterLoadAsync runs as part of SaveService.LoadAsync.
            save.RegisterHook(this);
        }

        public event Action<InventoryChangeEvent> Changed;

        // ----- ISaveHook -----

        public async UniTask AfterLoadAsync(CancellationToken ct)
        {
            var dto = await _repository.LoadAsync(ct);
            _uniques.Clear();
            _stacks.Clear();

            if (dto.Uniques != null)
            {
                for (var i = 0; i < dto.Uniques.Count; i++)
                {
                    var u = dto.Uniques[i];
                    if (u == null || string.IsNullOrEmpty(u.ItemId)) continue;
                    _uniques[u.ItemId] = u.CategoryId;
                }
            }

            if (dto.Stacks != null)
            {
                for (var i = 0; i < dto.Stacks.Count; i++)
                {
                    var s = dto.Stacks[i];
                    if (s == null || string.IsNullOrEmpty(s.ItemId) || s.Count <= 0) continue;
                    _stacks[s.ItemId] = (s.CategoryId, s.Count);
                }
            }

            _loaded = true;
            Debug.Log($"{LogPrefix} loaded: uniques={_uniques.Count}, stacks={_stacks.Count}.");
        }

        public UniTask BeforeSaveAsync(CancellationToken ct) => UniTask.CompletedTask;

        // ----- sync read -----

        public IReadOnlyList<InventoryItem> GetAll()
        {
            var result = new List<InventoryItem>(_uniques.Count + _stacks.Count);
            foreach (var kv in _uniques) result.Add(new InventoryItem(kv.Key, kv.Value, 1));
            foreach (var kv in _stacks) result.Add(new InventoryItem(kv.Key, kv.Value.CategoryId, kv.Value.Count));
            return result;
        }

        public IReadOnlyList<InventoryItem> GetByCategory(string categoryId)
        {
            var result = new List<InventoryItem>();
            if (string.IsNullOrEmpty(categoryId)) return result;

            foreach (var kv in _uniques)
                if (string.Equals(kv.Value, categoryId, StringComparison.Ordinal))
                    result.Add(new InventoryItem(kv.Key, kv.Value, 1));

            foreach (var kv in _stacks)
                if (string.Equals(kv.Value.CategoryId, categoryId, StringComparison.Ordinal))
                    result.Add(new InventoryItem(kv.Key, kv.Value.CategoryId, kv.Value.Count));

            return result;
        }

        public bool Has(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            return _uniques.ContainsKey(itemId) || _stacks.ContainsKey(itemId);
        }

        public int GetCount(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            if (_uniques.ContainsKey(itemId)) return 1;
            return _stacks.TryGetValue(itemId, out var entry) ? entry.Count : 0;
        }

        // ----- async write -----

        public async UniTask AddAsync(string itemId, string categoryId, int amount, CancellationToken ct)
        {
            if (!Validate(itemId, categoryId, out var category)) return;
            if (amount <= 0) return;

            var changed = ApplyAdd(itemId, category, amount, out var change);
            if (!changed) return;

            await PersistAsync(ct);
            Changed?.Invoke(change);
        }

        public async UniTask AddBatchAsync(IEnumerable<InventoryItem> items, CancellationToken ct)
        {
            if (items == null) return;

            var changes = new List<InventoryChangeEvent>();
            foreach (var item in items)
            {
                if (item == null) continue;
                if (!Validate(item.ItemId, item.CategoryId, out var category)) continue;
                var amount = item.Count <= 0 ? 1 : item.Count;
                if (ApplyAdd(item.ItemId, category, amount, out var change))
                    changes.Add(change);
            }

            if (changes.Count == 0) return;
            await PersistAsync(ct);
            for (var i = 0; i < changes.Count; i++) Changed?.Invoke(changes[i]);
        }

        public async UniTask<bool> RemoveAsync(string itemId, int amount, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(itemId) || amount <= 0) return false;

            if (_uniques.TryGetValue(itemId, out var uniqueCategory))
            {
                if (amount != 1) return false; // Unique entries cannot be partially removed.
                _uniques.Remove(itemId);
                await PersistAsync(ct);
                Changed?.Invoke(new InventoryChangeEvent(uniqueCategory, itemId, InventoryChangeKind.Removed, 0));
                return true;
            }

            if (_stacks.TryGetValue(itemId, out var stack))
            {
                if (amount > stack.Count) return false;
                var newCount = stack.Count - amount;
                if (newCount <= 0)
                {
                    _stacks.Remove(itemId);
                    await PersistAsync(ct);
                    Changed?.Invoke(new InventoryChangeEvent(stack.CategoryId, itemId, InventoryChangeKind.Removed, 0));
                }
                else
                {
                    _stacks[itemId] = (stack.CategoryId, newCount);
                    await PersistAsync(ct);
                    Changed?.Invoke(new InventoryChangeEvent(stack.CategoryId, itemId, InventoryChangeKind.Updated, newCount));
                }
                return true;
            }

            return false;
        }

        // ----- internals -----

        private bool Validate(string itemId, string categoryId, out ItemCategory category)
        {
            category = null;
            if (string.IsNullOrEmpty(itemId))
            {
                Debug.LogError($"{LogPrefix} Add rejected: empty itemId.");
                return false;
            }
            if (!_categories.TryGet(categoryId, out category))
            {
                Debug.LogError($"{LogPrefix} Add rejected: unknown category '{categoryId}' for item '{itemId}'.");
                return false;
            }
            if (!_loaded)
            {
                Debug.LogWarning($"{LogPrefix} Add called before AfterLoadAsync completed; mutation will still apply.");
            }
            return true;
        }

        private bool ApplyAdd(string itemId, ItemCategory category, int amount, out InventoryChangeEvent change)
        {
            if (category.StackingMode == ItemStackingMode.Unique)
            {
                if (_uniques.ContainsKey(itemId))
                {
                    change = null;
                    return false; // idempotent
                }
                _uniques[itemId] = category.Id;
                change = new InventoryChangeEvent(category.Id, itemId, InventoryChangeKind.Added, 1);
                return true;
            }

            // Stack
            if (_stacks.TryGetValue(itemId, out var existing))
            {
                var newCount = existing.Count + amount;
                _stacks[itemId] = (existing.CategoryId, newCount);
                change = new InventoryChangeEvent(category.Id, itemId, InventoryChangeKind.Updated, newCount);
            }
            else
            {
                _stacks[itemId] = (category.Id, amount);
                change = new InventoryChangeEvent(category.Id, itemId, InventoryChangeKind.Added, amount);
            }
            return true;
        }

        private UniTask PersistAsync(CancellationToken ct)
        {
            var dto = new InventoryStateDto();
            foreach (var kv in _uniques)
                dto.Uniques.Add(new InventoryStateDto.UniqueEntry { ItemId = kv.Key, CategoryId = kv.Value });
            foreach (var kv in _stacks)
                dto.Stacks.Add(new InventoryStateDto.StackEntry { ItemId = kv.Key, CategoryId = kv.Value.CategoryId, Count = kv.Value.Count });
            return _repository.SaveAsync(dto, ct);
        }
    }
}
