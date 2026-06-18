using Game.Rewards.API;

namespace Game.Shop.API
{
    /// <summary>
    /// Outcome of <see cref="IShopService.BuyAsync"/>. <see cref="Granted"/> is the actually-granted
    /// reward (post-expansion, e.g. rolled book contents for a book-box) — only populated on
    /// <see cref="ShopPurchaseStatus.Success"/>.
    /// </summary>
    public readonly struct ShopPurchaseResult
    {
        public ShopPurchaseStatus Status { get; }
        public ShopLot Lot { get; }
        public RewardSpec Granted { get; }

        private ShopPurchaseResult(ShopPurchaseStatus status, ShopLot lot, RewardSpec granted)
        {
            Status = status;
            Lot = lot;
            Granted = granted;
        }

        public static ShopPurchaseResult Ok(ShopLot lot, RewardSpec granted) =>
            new ShopPurchaseResult(ShopPurchaseStatus.Success, lot, granted);

        public static ShopPurchaseResult Fail(ShopPurchaseStatus status, ShopLot lot = null) =>
            new ShopPurchaseResult(status, lot, null);
    }
}
