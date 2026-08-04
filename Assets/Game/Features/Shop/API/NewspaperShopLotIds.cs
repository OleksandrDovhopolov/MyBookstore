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
        public const string BookBoxGeneral5 = "newspaper_box_general_5";
        public const string BookBoxGeneral10 = "newspaper_box_general_10";
        public const string BookBoxCommon15 = "newspaper_book_common_15";
        public const string BookBoxRare8 = "newspaper_book_rare_8";
        public const string BookBoxGenreClassic = "newspaper_box_genre_classic";
        public const string BookBoxGenreCrime = "newspaper_box_genre_crime";
        public const string BookBoxGenreDrama = "newspaper_box_genre_drama";
        public const string BookBoxGenreFact = "newspaper_box_genre_fact";
        public const string BookBoxGenreFantasy = "newspaper_box_genre_fantasy";
        public const string BookBoxGenreKids = "newspaper_box_genre_kids";
        public const string BookBoxGenreTravel = "newspaper_box_genre_travel";

        public const string StorefrontDecor = "newspaper.decor";
        public const string StorefrontBooks = "newspaper.books";
        public const string StorefrontConsumables = "newspaper.consumables";
    }
}
