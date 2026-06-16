namespace Game.Shop.API
{
    /// <summary>
    /// How a <see cref="ShopLot"/>'s purchase counter behaves. Phase 0 ships two modes; future
    /// daily/weekly refresh modes (Phase 1+) extend this enum.
    /// </summary>
    public enum ShopLimitMode
    {
        /// <summary>No cap on purchase count. Phase 0 default for book crates.</summary>
        Unlimited,

        /// <summary>Bought up to <see cref="ShopLotLimit.MaxPurchases"/> times, then permanently sold out.</summary>
        Disposable
    }
}
