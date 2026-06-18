using Game.Rewards.API;

namespace Game.Shop.API
{
    /// <summary>
    /// Fired by <see cref="IShopService.LotPurchased"/> after a successful buy. Lets UI / analytics /
    /// FTUE listen for purchases without coupling to the full pipeline.
    /// </summary>
    public readonly struct ShopPurchaseEvent
    {
        public ShopLot Lot { get; }
        public RewardSpec Granted { get; }

        public ShopPurchaseEvent(ShopLot lot, RewardSpec granted)
        {
            Lot = lot;
            Granted = granted;
        }
    }
}
