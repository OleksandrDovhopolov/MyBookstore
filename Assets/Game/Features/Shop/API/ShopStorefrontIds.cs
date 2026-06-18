namespace Game.Shop.API
{
    /// <summary>
    /// Well-known storefront ids used as the partition key in <see cref="IShopService.GetLots"/>.
    /// Lives in API so both shop config consumers (ShopService warmup) and UI consumers
    /// (NewspaperWindow, ClassicShopWindow) can reference a single source of truth.
    /// </summary>
    public static class ShopStorefrontIds
    {
        public const string NewspaperBooks = "newspaper.books";
        public const string NewspaperDecor = "newspaper.decor";

        public const string ClassicBooks = "classic.books";
        public const string ClassicBoxes = "classic.boxes";
        public const string ClassicDecor = "classic.decor";
    }
}
