using System;

namespace Game.Inventory.API
{
    /// <summary>
    /// Read-only projection of an inventory entry. For Unique categories <see cref="Count"/> is always 1.
    /// </summary>
    public sealed class InventoryItem
    {
        public string ItemId { get; }
        public string CategoryId { get; }
        public int Count { get; }

        public InventoryItem(string itemId, string categoryId, int count)
        {
            ItemId = itemId ?? throw new ArgumentNullException(nameof(itemId));
            CategoryId = categoryId ?? throw new ArgumentNullException(nameof(categoryId));
            Count = count;
        }
    }
}
