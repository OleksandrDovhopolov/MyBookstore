using System.Collections.Generic;
using System.Linq;

namespace Game.Shop.UI
{
    public static class ShopTabOffers
    {
        private static readonly ShopOffer[] Empty = { };

        public static IReadOnlyList<ShopOffer> Build(IShopOfferSource source, ShopTab tab)
        {
            if (source == null) return Empty;

            return tab switch
            {
                ShopTab.Boxes => OrEmpty(source.GetBookOffers()),
                ShopTab.Decor => SortDecorForDisplay(source.GetDecorOffers()),
                ShopTab.Consumable => OrEmpty(source.GetConsumableOffers()),
                _ => BuildAll(source),
            };
        }

        private static IReadOnlyList<ShopOffer> BuildAll(IShopOfferSource source)
        {
            var offers = new List<ShopOffer>();
            AddRange(offers, source.GetBookOffers());
            AddRange(offers, source.GetConsumableOffers());
            AddRange(offers, SortDecorForDisplay(source.GetDecorOffers()));
            return offers;
        }

        private static IReadOnlyList<ShopOffer> SortDecorForDisplay(IReadOnlyList<ShopOffer> offers)
        {
            if (offers == null || offers.Count == 0) return Empty;
            if (offers.Count == 1) return offers;

            return offers
                .OrderBy(offer => offer != null && offer.IsDecor && !offer.IsAvailable ? 1 : 0)
                .ToList();
        }

        private static IReadOnlyList<ShopOffer> OrEmpty(IReadOnlyList<ShopOffer> offers) =>
            offers == null || offers.Count == 0 ? Empty : offers;

        private static void AddRange(List<ShopOffer> target, IReadOnlyList<ShopOffer> offers)
        {
            if (target == null || offers == null || offers.Count == 0) return;

            for (var i = 0; i < offers.Count; i++)
                target.Add(offers[i]);
        }
    }
}
