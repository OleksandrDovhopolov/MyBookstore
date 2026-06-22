using Game.Configs.Models;
using Game.Rewards.API;
using Game.Shop.API;
using Game.Shop.Services;
using Game.Shop.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Game.Shop.Tests.Editor
{
    public sealed class ShopConfigRewardSpecProviderTests
    {
        private const string Gold = "gold";

        [Test]
        public void TryBuild_InlineRewardItems_BuildsRewardSpec()
        {
            var cfg = new ShopConfig
            {
                Id = "lot_a",
                StorefrontId = "store",
                RewardId = "reward_lot_a",
                RewardItems = new[]
                {
                    new RewardItemData { Id = Gold, Amount = 25, Kind = RewardKind.Resource },
                    new RewardItemData { Id = "decor_a", Category = "decor", Amount = 1, Kind = RewardKind.InventoryItem }
                }
            };
            var provider = Build(cfg);
            var lot = Lot(cfg);

            var ok = provider.TryBuild(lot, out var spec);

            Assert.IsTrue(ok);
            Assert.AreEqual("reward_lot_a", spec.Id);
            Assert.AreEqual(2, spec.Items.Count);
            Assert.AreEqual(RewardKind.Resource, spec.Items[0].Kind);
            Assert.AreEqual(Gold, spec.Items[0].Id);
            Assert.AreEqual(25, spec.Items[0].Amount);
            Assert.AreEqual(RewardKind.InventoryItem, spec.Items[1].Kind);
            Assert.AreEqual("decor_a", spec.Items[1].Id);
            Assert.AreEqual("decor", spec.Items[1].Category);
        }

        [Test]
        public void TryBuild_EmptyRewardItems_BuildsEmptySpec()
        {
            var cfg = new ShopConfig
            {
                Id = "lot_box",
                StorefrontId = "store",
                RewardId = "book_box_common_15",
                RewardItems = new RewardItemData[0]
            };
            var provider = Build(cfg);

            var ok = provider.TryBuild(Lot(cfg), out var spec);

            Assert.IsTrue(ok);
            Assert.AreEqual("book_box_common_15", spec.Id);
            Assert.AreEqual(0, spec.Items.Count);
        }

        [Test]
        public void TryBuild_NullRewardItems_BuildsEmptySpec()
        {
            var cfg = new ShopConfig
            {
                Id = "lot_box",
                StorefrontId = "store",
                RewardId = "book_box_common_15",
                RewardItems = null
            };
            var provider = Build(cfg);

            var ok = provider.TryBuild(Lot(cfg), out var spec);

            Assert.IsTrue(ok);
            Assert.AreEqual("book_box_common_15", spec.Id);
            Assert.AreEqual(0, spec.Items.Count);
        }

        [Test]
        public void TryBuild_MissingConfig_ReturnsFalse()
        {
            var provider = Build();
            var lot = new ShopLot(
                "missing_lot",
                "store",
                new ShopPrice(Gold, 10),
                "reward_missing",
                ShopLotLimit.Unlimited());

            var ok = provider.TryBuild(lot, out var spec);

            Assert.IsFalse(ok);
            Assert.IsNull(spec);
        }

        [Test]
        public void TryBuild_UsesShopLotRewardId()
        {
            var cfg = new ShopConfig
            {
                Id = "lot_a",
                StorefrontId = "store",
                RewardId = "config_reward",
                RewardItems = new RewardItemData[0]
            };
            var provider = Build(cfg);
            var lot = new ShopLot(
                cfg.Id,
                cfg.StorefrontId,
                new ShopPrice(Gold, 10),
                "lot_reward",
                ShopLotLimit.Unlimited());

            provider.TryBuild(lot, out var spec);

            Assert.AreEqual("lot_reward", spec.Id);
        }

        private static ShopConfigRewardSpecProvider Build(params ShopConfig[] configs)
        {
            var fake = new FakeConfigsService();
            fake.Seed(configs);
            return new ShopConfigRewardSpecProvider(fake);
        }

        private static ShopLot Lot(ShopConfig cfg) =>
            new ShopLot(
                cfg.Id,
                cfg.StorefrontId,
                new ShopPrice(Gold, 10),
                cfg.RewardId,
                ShopLotLimit.Unlimited());
    }
}
