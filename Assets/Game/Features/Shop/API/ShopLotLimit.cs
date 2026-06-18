namespace Game.Shop.API
{
    /// <summary>
    /// Purchase limit settings for a <see cref="ShopLot"/>. <see cref="MaxPurchases"/> is only read
    /// when <see cref="Mode"/> is <see cref="ShopLimitMode.Disposable"/>; <c>null</c> for
    /// <see cref="ShopLimitMode.Unlimited"/>.
    /// </summary>
    public readonly struct ShopLotLimit
    {
        public ShopLimitMode Mode { get; }
        public int? MaxPurchases { get; }

        public ShopLotLimit(ShopLimitMode mode, int? maxPurchases)
        {
            Mode = mode;
            MaxPurchases = maxPurchases;
        }

        public static ShopLotLimit Unlimited() => new ShopLotLimit(ShopLimitMode.Unlimited, null);

        public static ShopLotLimit Disposable(int maxPurchases) =>
            new ShopLotLimit(ShopLimitMode.Disposable, maxPurchases);
    }
}
