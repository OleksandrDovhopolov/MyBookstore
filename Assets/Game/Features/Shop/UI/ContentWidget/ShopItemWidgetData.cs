using Game.UI.ContentWidget;

namespace Game.Shop.UI
{
    public sealed class ShopItemWidgetData : ContentWidgetDataBase
    {
        public ShopItemWidgetData(string lotId, string description)
        {
            LotId = lotId;
            Description = description;
        }

        public string LotId { get; }
        public string Description { get; }
    }
}
