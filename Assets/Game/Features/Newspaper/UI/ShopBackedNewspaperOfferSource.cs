using System;
using System.Collections.Generic;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using Game.Shop.API;
using UnityEngine;

namespace Game.Newspaper.UI
{
    public sealed class ShopBackedNewspaperOfferSource : INewspaperOfferSource
    {
        // Shared sprite id for every book-box offer (book offers do not have a per-lot icon).
        private const string BookOfferIconId = "book_box";

        private const string NewState = "NEW!";
        private const string SoldState = "SOLD";
        private const string FreePrice = "FREE";

        private readonly IShopService _shop;
        private readonly IConfigsService _configs;

        public ShopBackedNewspaperOfferSource(IShopService shop, IConfigsService configs)
        {
            _shop = shop ?? throw new ArgumentNullException(nameof(shop));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        public IReadOnlyList<NewspaperOffer> GetBookOffers() =>
            BuildOffers(NewspaperShopLotIds.StorefrontBooks, isDecor: false);

        public IReadOnlyList<NewspaperOffer> GetDecorOffers() =>
            BuildOffers(NewspaperShopLotIds.StorefrontDecor, isDecor: true);

        private IReadOnlyList<NewspaperOffer> BuildOffers(string storefrontId, bool isDecor)
        {
            var lots = _shop.GetLots(storefrontId);
            if (lots == null || lots.Count == 0) return Array.Empty<NewspaperOffer>();

            var offers = new List<NewspaperOffer>(lots.Count);
            for (var i = 0; i < lots.Count; i++)
            {
                var lot = lots[i];
                if (lot == null) continue;

                var isAvailable = _shop.IsAvailable(lot.LotId);
                var iconId = isDecor ? ResolveDecorIconId(lot.LotId) : BookOfferIconId;
                offers.Add(new NewspaperOffer(
                    lot.LotId,
                    iconId,
                    string.IsNullOrEmpty(lot.DisplayName) ? lot.RewardId : lot.DisplayName,
                    lot.Description ?? string.Empty,
                    FormatPrice(lot.Price),
                    isAvailable,
                    isAvailable ? NewState : SoldState));
            }

            return offers;
        }

        // Decor icons are addressed by the decors.json id (the lot's decor reward item id), the same
        // id RewardsWindow uses — never the shop lot id (shop.json). Falls back to the lot id.
        private string ResolveDecorIconId(string lotId)
        {
            if (_configs.TryGet<ShopConfig>(lotId, out var cfg) && cfg?.RewardItems != null)
            {
                for (var i = 0; i < cfg.RewardItems.Length; i++)
                {
                    var item = cfg.RewardItems[i];
                    if (item != null
                        && string.Equals(item.Category, InventoryCategories.Decor, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrEmpty(item.Id))
                        return item.Id;
                }
            }

            Debug.LogWarning(
                $"[ShopBackedNewspaperOfferSource] No decor reward item for lot '{lotId}'. Falling back to lot id.");
            return lotId;
        }

        private static string FormatPrice(ShopPrice price)
        {
            if (price.Amount <= 0) return FreePrice;
            return price.Amount.ToString();
        }
    }
}
