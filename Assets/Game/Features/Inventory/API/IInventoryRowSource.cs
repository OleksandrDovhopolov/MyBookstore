using System.Collections.Generic;

namespace Game.Inventory.API
{
    public interface IInventoryRowSource
    {
        int Order { get; }
        string CategoryId { get; }
        IEnumerable<InventoryRowModel> BuildRows();
    }
}
