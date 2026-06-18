namespace Game.Shop.API
{
    public enum ShopPurchaseStatus
    {
        Success,
        NotEnoughCurrency,
        LimitReached,
        LotNotFound,
        /// <summary>
        /// The lot's inline rewardItems include a Unique-category InventoryItem the player already
        /// owns (e.g., a single-book classic lot whose book_id is already in inventory). Phase 1
        /// fix — book-box lots are excluded because their expander filters owned items separately.
        /// </summary>
        AlreadyOwned,
        InternalError
    }
}
