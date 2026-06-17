using System;
using System.Collections.Generic;
using Analytics;
using Game.Shop.API;
using VContainer.Unity;

namespace Game.Shop.Services
{
    /// <summary>
    /// Subscribes to <see cref="IShopService.LotPurchased"/> and forwards each purchase to
    /// <see cref="IAnalyticsService"/> as an <c>item_purchased</c> event. Internal — exposed only
    /// through DI as <see cref="IStartable"/> so VContainer constructs it at scope start.
    /// </summary>
    /// <remarks>
    /// Phase 1 scope: one event family (<c>item_purchased</c>). <c>shop_window_opened</c> and
    /// <c>shop_lot_failed</c> require call sites in UI / ShopService respectively — added incrementally
    /// when Classic Shop window lands (PR11) and when we want to track NotEnoughCurrency funnels.
    /// </remarks>
    internal sealed class ShopAnalyticsListener : IStartable, IDisposable
    {
        private readonly IShopService _shop;
        private readonly IAnalyticsService _analytics;

        public ShopAnalyticsListener(IShopService shop, IAnalyticsService analytics)
        {
            _shop = shop ?? throw new ArgumentNullException(nameof(shop));
            _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
        }

        public void Start()
        {
            _shop.LotPurchased += OnLotPurchased;
        }

        public void Dispose()
        {
            if (_shop != null) _shop.LotPurchased -= OnLotPurchased;
        }

        private void OnLotPurchased(ShopPurchaseEvent evt)
        {
            var lot = evt.Lot;
            if (lot == null) return;

            var parameters = new Dictionary<string, object>
            {
                ["lot_id"] = lot.LotId,
                ["storefront_id"] = lot.StorefrontId,
                ["reward_id"] = lot.RewardId,
                ["price_currency"] = lot.Price.Currency,
                ["price_amount"] = lot.Price.Amount,
                ["granted_item_count"] = evt.Granted?.Items?.Count ?? 0,
            };

            _analytics.TrackEvent(new AnalyticsEvent(AnalyticsEventNames.ItemPurchased, parameters));
        }
    }
}
