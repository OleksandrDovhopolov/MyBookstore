namespace Game.Shop.API
{
    /// <summary>
    /// Well-known lot ids for the Newspaper storefronts. Lives in <c>Game.Shop.API</c> so that
    /// <see cref="ShopService"/> (legacy decor migration), <c>DecorRewardService</c> facade, and
    /// <c>NewspaperWindow</c> can reference one source of truth without cross-feature deps.
    /// </summary>
    public static class NewspaperShopLotIds
    {
        public const string DecorFreeVintageGlobe = "newspaper_decor_vintage_globe";
        public const string DecorPaidCoffeePot = "newspaper_decor_coffee_pot";

        public const string StorefrontDecor = "newspaper.decor";
        public const string StorefrontBooks = "newspaper.books";
    }
}
