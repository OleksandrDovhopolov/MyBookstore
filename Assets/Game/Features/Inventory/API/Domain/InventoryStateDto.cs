using System.Collections.Generic;

namespace Game.Inventory.API
{
    /// <summary>
    /// Transport DTO between <see cref="IInventoryService"/> and <see cref="IInventoryRepository"/>.
    /// Same shape as the persisted POCO; lives in the API assembly so a server-backed repository
    /// can be authored without referencing the implementation.
    /// </summary>
    public sealed class InventoryStateDto
    {
        public List<UniqueEntry> Uniques { get; set; } = new();
        public List<StackEntry> Stacks { get; set; } = new();

        public sealed class UniqueEntry
        {
            public string ItemId { get; set; }
            public string CategoryId { get; set; }
        }

        public sealed class StackEntry
        {
            public string ItemId { get; set; }
            public string CategoryId { get; set; }
            public int Count { get; set; }
        }
    }
}
