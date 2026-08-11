using System;
using System.Collections.Generic;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using Game.Rewards.API;
using Game.Rewards.Services;
using Game.Shop.API;
using UnityEngine;

namespace Game.Shop.UI
{
    public sealed class ShopOfferSource : IShopOfferSource
    {
        private const string BookOfferIconId = "book_box";

        private const string NewState = "NEW!";
        private const string SoldState = "SOLD";
        private const string FreePrice = "FREE";

        private readonly IShopService _shop;
        private readonly IConfigsService _configs;

        public ShopOfferSource(IShopService shop, IConfigsService configs)
        {
            _shop = shop ?? throw new ArgumentNullException(nameof(shop));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        public IReadOnlyList<ShopOffer> GetBookOffers() =>
            BuildOffers(NewspaperShopLotIds.StorefrontBooks, isDecor: false);

        public IReadOnlyList<ShopOffer> GetDecorOffers() =>
            BuildOffers(NewspaperShopLotIds.StorefrontDecor, isDecor: true, InventoryCategories.Decor);

        public IReadOnlyList<ShopOffer> GetConsumableOffers() =>
            BuildOffers(NewspaperShopLotIds.StorefrontConsumables, isDecor: false, InventoryCategories.Consumable);

        private IReadOnlyList<ShopOffer> BuildOffers(string storefrontId, bool isDecor, string rewardCategoryId = null)
        {
            var lots = string.Equals(storefrontId, NewspaperShopLotIds.StorefrontBooks, StringComparison.Ordinal)
                ? _shop.GetOfferedLots(storefrontId)
                : _shop.GetLots(storefrontId);
            if (lots == null || lots.Count == 0) return Array.Empty<ShopOffer>();

            var offers = new List<ShopOffer>(lots.Count);
            for (var i = 0; i < lots.Count; i++)
            {
                var lot = lots[i];
                if (lot == null) continue;

                var isAvailable = _shop.IsAvailable(lot.LotId);
                var iconId = ResolveRewardItemIconId(lot.LotId, rewardCategoryId)
                             ?? ResolveDefaultIconId(lot, isDecor);
                var bookIconId = ResolveBookIconId(lot, isDecor);
                offers.Add(new ShopOffer(
                    lot.LotId,
                    iconId,
                    string.IsNullOrEmpty(lot.DisplayName) ? lot.RewardId : lot.DisplayName,
                    lot.Description ?? string.Empty,
                    FormatPrice(lot.Price),
                    isAvailable,
                    isAvailable ? NewState : SoldState,
                    isDecor,
                    bookIconId));
            }

            return offers;
        }

        private string ResolveRewardItemIconId(string lotId, string categoryId)
        {
            if (string.IsNullOrEmpty(categoryId)) return null;

            if (_configs.TryGet<ShopConfig>(lotId, out var cfg) && cfg?.RewardItems != null)
            {
                string firstInventoryItemId = null;

                for (var i = 0; i < cfg.RewardItems.Length; i++)
                {
                    var item = cfg.RewardItems[i];
                    if (item == null
                        || item.Kind != RewardKind.InventoryItem
                        || string.IsNullOrEmpty(item.Id))
                        continue;

                    firstInventoryItemId ??= item.Id;

                    if (string.Equals(item.Category, categoryId, StringComparison.OrdinalIgnoreCase))
                        return item.Id;
                }

                if (!string.IsNullOrEmpty(firstInventoryItemId))
                    return firstInventoryItemId;
            }

            Debug.LogWarning(
                $"[ShopBackedNewspaperOfferSource] No inventory reward item for lot '{lotId}'.");
            return null;
        }

        private static string ResolveDefaultIconId(ShopLot lot, bool isDecor)
        {
            if (isDecor) return lot.LotId;

            return BookOfferIconId;
        }

        private static string ResolveBookIconId(ShopLot lot, bool isDecor)
        {
            if (isDecor || lot == null) return null;

            if (BookBoxPoolRules.TryGet(lot.RewardId, out var rule)
                && rule.Kind == BookBoxKind.Genre
                && !string.IsNullOrEmpty(rule.Genre))
                return rule.Genre;

            return null;
        }

        private static string FormatPrice(ShopPrice price)
        {
            if (price.Amount <= 0) return FreePrice;
            return price.Amount.ToString();
        }
    }
}
