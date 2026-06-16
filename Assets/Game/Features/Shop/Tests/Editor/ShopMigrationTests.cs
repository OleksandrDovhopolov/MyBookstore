using System.Collections.Generic;
using System.Threading;
using Game.Configs.Models;
using Game.Decor;
using Game.Rewards.API;
using Game.Shop.API;
using Game.Shop.Services;
using Game.Shop.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Game.Shop.Tests.Editor
{
    public sealed class ShopMigrationTests
    {
        private const string Gold = "gold";
        private const string DecorStorefront = "newspaper.decor";

        private static ShopConfig DecorLot(string id, int price) =>
            new ShopConfig
            {
                Id = id,
                StorefrontId = DecorStorefront,
                Price = new ShopPriceData { Currency = Gold, Amount = price },
                RewardId = "reward_" + id,
                RewardItems = new[]
                {
                    new RewardItemData { Id = id + "_item", Category = "decor", Amount = 1, Kind = RewardKind.InventoryItem }
                },
                Limit = new ShopLotLimitData { Mode = ShopLimitMode.Disposable, MaxPurchases = 1 }
            };

        private static IReadOnlyList<ShopConfig> DecorLots() => new[]
        {
            DecorLot(NewspaperShopLotIds.DecorFreeVintageGlobe, 0),
            DecorLot(NewspaperShopLotIds.DecorPaidCoffeePot, 50)
        };

        private sealed class Harness
        {
            public ShopService Svc;
            public FakeSaveService Save;
            public FakeResourcesService Resources;
            public FakeRewardGrantService Rewards;
            public FakeConfigsService Configs;
            public SaveBackedShopRepository Repo;
        }

        private static Harness Build()
        {
            var h = new Harness
            {
                Save = new FakeSaveService(),
                Resources = new FakeResourcesService(),
                Rewards = new FakeRewardGrantService(),
                Configs = new FakeConfigsService()
            };
            h.Configs.Seed(DecorLots());
            h.Repo = new SaveBackedShopRepository(h.Save);
            h.Svc = new ShopService(h.Save, h.Repo, h.Resources, h.Rewards, h.Configs);
            return h;
        }

        [Test]
        public void Migration_LegacyFreeClaimed_MarksFreeLotPurchased()
        {
            var h = Build();
            h.Save.Seed(DecorSaveKeys.Placement, new DecorPlacementState { FirstDayRewardClaimed = true });

            h.Svc.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(1, h.Svc.GetPurchaseCount(NewspaperShopLotIds.DecorFreeVintageGlobe));
            Assert.IsFalse(h.Svc.IsAvailable(NewspaperShopLotIds.DecorFreeVintageGlobe));
            Assert.AreEqual(0, h.Svc.GetPurchaseCount(NewspaperShopLotIds.DecorPaidCoffeePot));
            Assert.IsTrue(h.Svc.IsAvailable(NewspaperShopLotIds.DecorPaidCoffeePot));
        }

        [Test]
        public void Migration_LegacyPaidPurchased_MarksPaidLotPurchased()
        {
            var h = Build();
            h.Save.Seed(DecorSaveKeys.Placement, new DecorPlacementState { FirstDayPurchaseDone = true });

            h.Svc.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(0, h.Svc.GetPurchaseCount(NewspaperShopLotIds.DecorFreeVintageGlobe));
            Assert.IsTrue(h.Svc.IsAvailable(NewspaperShopLotIds.DecorFreeVintageGlobe));
            Assert.AreEqual(1, h.Svc.GetPurchaseCount(NewspaperShopLotIds.DecorPaidCoffeePot));
            Assert.IsFalse(h.Svc.IsAvailable(NewspaperShopLotIds.DecorPaidCoffeePot));
        }

        [Test]
        public void Migration_BothLegacyFlagsSet_MarksBothLots()
        {
            var h = Build();
            h.Save.Seed(DecorSaveKeys.Placement, new DecorPlacementState
            {
                FirstDayRewardClaimed = true,
                FirstDayPurchaseDone = true
            });

            h.Svc.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(h.Svc.IsAvailable(NewspaperShopLotIds.DecorFreeVintageGlobe));
            Assert.IsFalse(h.Svc.IsAvailable(NewspaperShopLotIds.DecorPaidCoffeePot));
        }

        [Test]
        public void Migration_NoLegacyDto_DoesNothing()
        {
            var h = Build();
            // Intentionally no Seed of "decor.placement" — emulates a fresh install.

            h.Svc.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(h.Svc.IsAvailable(NewspaperShopLotIds.DecorFreeVintageGlobe));
            Assert.IsTrue(h.Svc.IsAvailable(NewspaperShopLotIds.DecorPaidCoffeePot));
            Assert.IsFalse(h.Save.Store.ContainsKey(ShopSaveKeys.State),
                "Migration must not persist a shop module on a fresh install.");
        }

        [Test]
        public void Migration_AlreadyMigratedShopState_SkipsRewrite()
        {
            var h = Build();
            // Existing shop state already has the free lot — migration must bail out and NOT touch
            // the paid lot, even though the legacy flag for it is set.
            h.Save.Seed(ShopSaveKeys.State, new ShopStateDto
            {
                Lots = new Dictionary<string, LotPurchasesDto>
                {
                    [NewspaperShopLotIds.DecorFreeVintageGlobe] = new LotPurchasesDto { Purchases = 1 }
                }
            });
            h.Save.Seed(DecorSaveKeys.Placement, new DecorPlacementState
            {
                FirstDayRewardClaimed = true,
                FirstDayPurchaseDone = true
            });

            h.Svc.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(h.Svc.IsAvailable(NewspaperShopLotIds.DecorFreeVintageGlobe),
                "Existing free-lot purchase carries over.");
            Assert.IsTrue(h.Svc.IsAvailable(NewspaperShopLotIds.DecorPaidCoffeePot),
                "Paid lot must NOT be migrated when shop state already has any decor entry.");
        }
    }
}
