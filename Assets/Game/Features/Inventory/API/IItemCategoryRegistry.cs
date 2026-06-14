using System.Collections.Generic;

namespace Game.Inventory.API
{
    /// <summary>
    /// Lookup table for inventory categories registered at startup. The inventory service
    /// consults it to decide how Add/Remove should treat an item id (Unique vs Stack).
    /// </summary>
    public interface IItemCategoryRegistry
    {
        void Register(ItemCategory category);

        /// <summary>Returns the category, or null when nothing is registered for that id.</summary>
        ItemCategory Get(string categoryId);

        bool TryGet(string categoryId, out ItemCategory category);

        IReadOnlyList<ItemCategory> GetAll();
    }
}
