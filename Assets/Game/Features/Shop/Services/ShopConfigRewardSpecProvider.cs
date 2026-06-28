using System;
using Game.Configs;
using Game.Configs.Models;
using Game.Rewards.API;
using Game.Shop.API;

namespace Game.Shop.Services
{
    public sealed class ShopConfigRewardSpecProvider : IShopRewardSpecProvider
    {
        private readonly IConfigsService _configs;

        public ShopConfigRewardSpecProvider(IConfigsService configs)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        public bool TryBuild(ShopLot lot, out RewardSpec spec)
        {
            spec = null;
            if (lot == null) return false;
            if (!_configs.TryGet<ShopConfig>(lot.LotId, out var cfg) || cfg == null) return false;

            spec = BuildSpec(cfg, lot);
            return true;
        }

        private static RewardSpec BuildSpec(ShopConfig cfg, ShopLot lot)
        {
            var items = cfg.RewardItems;
            if (items == null || items.Length == 0)
                return new RewardSpec(lot.RewardId, Array.Empty<RewardItem>());

            var rewardItems = new RewardItem[items.Length];
            for (var i = 0; i < items.Length; i++)
            {
                var src = items[i];
                rewardItems[i] = src.Kind == RewardKind.Resource
                    ? RewardItem.Resource(src.Id, src.Amount)
                    : RewardItem.InventoryItem(src.Id, src.Category, src.Amount);
            }
            return new RewardSpec(lot.RewardId, rewardItems);
        }
    }
}
