using Game.UI.ContentWidget;

namespace Game.Inventory.UI
{
    public sealed class InventoryItemWidgetData : ContentWidgetDataBase
    {
        public InventoryItemWidgetData(string itemId, string description)
        {
            ItemId = itemId;
            Description = description;
        }

        public string ItemId { get; }
        public string Description { get; }
    }
}
