namespace Game.Inventory.API
{
    public enum InventoryChangeKind
    {
        Added = 0,
        Removed = 1,
        Updated = 2
    }

    /// <summary>
    /// Notification published after a successful Add/Remove operation. Subscribers can decide
    /// whether to refresh views or react with side effects.
    /// </summary>
    public sealed class InventoryChangeEvent
    {
        public string CategoryId { get; }
        public string ItemId { get; }
        public InventoryChangeKind Kind { get; }

        /// <summary>New count after the change. 0 means the entry was removed entirely.</summary>
        public int NewCount { get; }

        public InventoryChangeEvent(string categoryId, string itemId, InventoryChangeKind kind, int newCount)
        {
            CategoryId = categoryId;
            ItemId = itemId;
            Kind = kind;
            NewCount = newCount;
        }
    }
}
