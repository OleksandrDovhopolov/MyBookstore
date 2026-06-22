using System.Collections.Generic;

namespace Game.Newspaper.UI
{
    public interface INewspaperOfferSource
    {
        IReadOnlyList<NewspaperOffer> GetBookOffers();
        IReadOnlyList<NewspaperOffer> GetDecorOffers();
    }
}
