using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Inventory.API;

namespace Game.Rewards.Tests.Editor.Fakes
{
    internal sealed class FakeInventoryService : IInventoryService
    {
        private readonly List<InventoryItem> _items = new List<InventoryItem>();

        public List<(string id, string category, int amount)> AddCalls { get; } =
            new List<(string id, string category, int amount)>();

        public event Action<InventoryChangeEvent> Changed;

        public IReadOnlyList<InventoryItem> GetAll() => _items;

        public IReadOnlyList<InventoryItem> GetByCategory(string categoryId) =>
            _items.Where(i => i.CategoryId == categoryId).ToList();

        public bool Has(string itemId) => GetCount(itemId) > 0;

        public int GetCount(string itemId)
        {
            var existing = _items.FirstOrDefault(i => i.ItemId == itemId);
            return existing?.Count ?? 0;
        }

        public UniTask AddAsync(string itemId, string categoryId, int amount, CancellationToken ct)
        {
            AddCalls.Add((itemId, categoryId, amount));
            var existing = _items.FirstOrDefault(i => i.ItemId == itemId);
            var newCount = (existing?.Count ?? 0) + amount;
            if (existing != null) _items.Remove(existing);
            _items.Add(new InventoryItem(itemId, categoryId, newCount));
            Changed?.Invoke(new InventoryChangeEvent(categoryId, itemId, InventoryChangeKind.Added, newCount));
            return UniTask.CompletedTask;
        }

        public UniTask AddBatchAsync(IEnumerable<InventoryItem> items, CancellationToken ct)
        {
            foreach (var item in items)
                AddAsync(item.ItemId, item.CategoryId, item.Count, ct).GetAwaiter().GetResult();
            return UniTask.CompletedTask;
        }

        public UniTask<bool> RemoveAsync(string itemId, int amount, CancellationToken ct)
        {
            var existing = _items.FirstOrDefault(i => i.ItemId == itemId);
            if (existing == null || existing.Count < amount) return UniTask.FromResult(false);
            _items.Remove(existing);
            var newCount = existing.Count - amount;
            if (newCount > 0)
                _items.Add(new InventoryItem(itemId, existing.CategoryId, newCount));
            return UniTask.FromResult(true);
        }
    }
}
