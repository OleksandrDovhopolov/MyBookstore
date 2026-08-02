using System.Collections.Generic;

namespace Game.Shop.UI
{
    public interface IShopOfferSource
    {
        IReadOnlyList<ShopOffer> GetBookOffers();
        IReadOnlyList<ShopOffer> GetDecorOffers();
        IReadOnlyList<ShopOffer> GetConsumableOffers();
    }
}
