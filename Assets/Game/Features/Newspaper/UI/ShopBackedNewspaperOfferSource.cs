using System;
using System.Collections.Generic;
using Game.Shop.API;

namespace Game.Newspaper.UI
{
    public sealed class ShopBackedNewspaperOfferSource : INewspaperOfferSource
    {
        private const string NewState = "NEW!";
        private const string SoldState = "SOLD";
        private const string FreePrice = "FREE";

        private readonly IShopService _shop;

        public ShopBackedNewspaperOfferSource(IShopService shop)
        {
            _shop = shop ?? throw new ArgumentNullException(nameof(shop));
        }

        public IReadOnlyList<NewspaperOffer> GetBookOffers() =>
            BuildOffers(NewspaperShopLotIds.StorefrontBooks);

        public IReadOnlyList<NewspaperOffer> GetDecorOffers() =>
            BuildOffers(NewspaperShopLotIds.StorefrontDecor);

        private IReadOnlyList<NewspaperOffer> BuildOffers(string storefrontId)
        {
            var lots = _shop.GetLots(storefrontId);
            if (lots == null || lots.Count == 0) return Array.Empty<NewspaperOffer>();

            var offers = new List<NewspaperOffer>(lots.Count);
            for (var i = 0; i < lots.Count; i++)
            {
                var lot = lots[i];
                if (lot == null) continue;

                var isAvailable = _shop.IsAvailable(lot.LotId);
                offers.Add(new NewspaperOffer(
                    lot.LotId,
                    string.IsNullOrEmpty(lot.DisplayName) ? lot.RewardId : lot.DisplayName,
                    lot.Description ?? string.Empty,
                    FormatPrice(lot.Price),
                    isAvailable,
                    isAvailable ? NewState : SoldState));
            }

            return offers;
        }

        private static string FormatPrice(ShopPrice price)
        {
            if (price.Amount <= 0) return FreePrice;
            return price.Amount.ToString();
        }
    }
}
