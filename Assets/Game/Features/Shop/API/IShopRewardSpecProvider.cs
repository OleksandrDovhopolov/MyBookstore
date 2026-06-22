using Game.Rewards.API;

namespace Game.Shop.API
{
    public interface IShopRewardSpecProvider
    {
        bool TryBuild(ShopLot lot, out RewardSpec spec);
    }
}
