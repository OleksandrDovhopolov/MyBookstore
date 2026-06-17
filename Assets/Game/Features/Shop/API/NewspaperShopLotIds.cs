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

        //TODO this values(id) should be inserted in view and constants be removed ? 
        public const string BookBoxCommon15 = "newspaper_book_common_15";
        public const string BookBoxRare8 = "newspaper_book_rare_8";
        public const string BookBoxGenreDystopic1 = "newspaper_book_genre_dystopic_1";

        public const string StorefrontDecor = "newspaper.decor";
        public const string StorefrontBooks = "newspaper.books";
    }
}
