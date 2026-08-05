using Game.Inventory.API;
using UIShared;

namespace Game.Inventory.UI
{
    public static class InventoryTabCategories
    {
        public static string Resolve(TabType tab)
        {
            return tab switch
            {
                TabType.Books => InventoryCategories.Book,
                TabType.Decor => InventoryCategories.Decor,
                TabType.Consumable => InventoryCategories.Consumable,
                _ => null
            };
        }
    }
}
