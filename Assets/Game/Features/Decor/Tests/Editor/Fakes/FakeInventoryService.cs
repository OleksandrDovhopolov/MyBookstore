using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Inventory.API;

namespace Game.Decor.Tests.Editor.Fakes
{
    public sealed class FakeInventoryService : IInventoryService
    {
        private readonly List<InventoryItem> _items = new();

        public event Action<InventoryChangeEvent> Changed;

        public void Seed(string itemId, string categoryId, int count = 1)
            => _items.Add(new InventoryItem(itemId, categoryId, count));

        public IReadOnlyList<InventoryItem> GetAll() => _items;

        public IReadOnlyList<InventoryItem> GetByCategory(string categoryId)
            => _items.Where(i => i.CategoryId == categoryId).ToList();

        public bool Has(string itemId) => _items.Any(i => i.ItemId == itemId);

        public int GetCount(string itemId) => _items.FirstOrDefault(i => i.ItemId == itemId)?.Count ?? 0;

        public UniTask AddAsync(string itemId, string categoryId, int amount, CancellationToken ct)
        {
            if (!Has(itemId)) Seed(itemId, categoryId, amount);
            Changed?.Invoke(new InventoryChangeEvent(categoryId, itemId, InventoryChangeKind.Added, amount));
            return UniTask.CompletedTask;
        }

        public UniTask AddBatchAsync(IEnumerable<InventoryItem> items, CancellationToken ct)
        {
            foreach (var i in items) Seed(i.ItemId, i.CategoryId, i.Count);
            return UniTask.CompletedTask;
        }

        public UniTask<bool> RemoveAsync(string itemId, int amount, CancellationToken ct)
        {
            var idx = _items.FindIndex(i => i.ItemId == itemId);
            if (idx < 0) return UniTask.FromResult(false);
            var item = _items[idx];
            _items.RemoveAt(idx);
            Changed?.Invoke(new InventoryChangeEvent(item.CategoryId, itemId, InventoryChangeKind.Removed, 0));
            return UniTask.FromResult(true);
        }
    }
}
