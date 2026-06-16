using System;

namespace Game.Shop.API
{
    /// <summary>
    /// One offer in a storefront. Static (config-driven) shape; runtime state (purchase count) lives
    /// in <see cref="IShopService"/>'s in-memory cache and persisted save module.
    /// </summary>
    public sealed class ShopLot
    {
        public string LotId { get; }
        public string StorefrontId { get; }
        public ShopPrice Price { get; }
        public string RewardId { get; }
        public ShopLotLimit Limit { get; }

        public ShopLot(string lotId, string storefrontId, ShopPrice price, string rewardId, ShopLotLimit limit)
        {
            LotId = lotId ?? throw new ArgumentNullException(nameof(lotId));
            StorefrontId = storefrontId ?? throw new ArgumentNullException(nameof(storefrontId));
            Price = price;
            RewardId = rewardId ?? throw new ArgumentNullException(nameof(rewardId));
            Limit = limit;
        }
    }
}
