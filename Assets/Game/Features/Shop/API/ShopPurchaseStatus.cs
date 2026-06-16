namespace Game.Shop.API
{
    public enum ShopPurchaseStatus
    {
        Success,
        NotEnoughCurrency,
        LimitReached,
        LotNotFound,
        InternalError
    }
}
