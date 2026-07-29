using System.Collections.Generic;

namespace Game.Inventory.API
{
    public interface IInventoryRowSource
    {
        int Order { get; }
        IEnumerable<InventoryRowModel> BuildRows();
    }
}
