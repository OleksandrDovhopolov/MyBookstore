using System.Collections.Generic;

namespace Game.Newspaper.UI
{
    public interface IShopOfferSource
    {
        IReadOnlyList<ShopOffer> GetBookOffers();
        IReadOnlyList<ShopOffer> GetDecorOffers();
    }
}
