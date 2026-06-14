using System;
using System.Collections.Generic;
using Game.Inventory.API;

namespace Game.Inventory.Services
{
    /// <inheritdoc cref="IItemCategoryRegistry"/>
    public sealed class ItemCategoryRegistry : IItemCategoryRegistry
    {
        private readonly Dictionary<string, ItemCategory> _byId = new(StringComparer.Ordinal);
        private readonly List<ItemCategory> _ordered = new();

        public void Register(ItemCategory category)
        {
            if (category == null) throw new ArgumentNullException(nameof(category));
            if (_byId.ContainsKey(category.Id))
                throw new InvalidOperationException($"Inventory category '{category.Id}' is already registered.");
            _byId.Add(category.Id, category);
            _ordered.Add(category);
        }

        public ItemCategory Get(string categoryId)
        {
            if (string.IsNullOrEmpty(categoryId)) return null;
            return _byId.TryGetValue(categoryId, out var c) ? c : null;
        }

        public bool TryGet(string categoryId, out ItemCategory category)
        {
            category = Get(categoryId);
            return category != null;
        }

        public IReadOnlyList<ItemCategory> GetAll() => _ordered;
    }
}
