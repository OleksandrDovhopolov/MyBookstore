using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Game.Configs.Models;
using Game.Rewards.API;
using Game.Shop.API;
using Game.Shop.Services;
using Game.Shop.Tests.Editor.Fakes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Shop.Tests.Editor
{
    public sealed class ShopServiceTests
    {
        private const string Gold = "gold";
        private const string Storefront = "newspaper.decor";

        private static ShopConfig DecorLot(string id, int price, ShopLimitMode mode = ShopLimitMode.Disposable, int max = 1) =>
            new ShopConfig
            {
                Id = id,
                StorefrontId = Storefront,
                Price = new ShopPriceData { Currency = Gold, Amount = price },
                RewardId = "reward_" + id,
                RewardItems = new[]
                {
                    new RewardItemData { Id = id + "_item", Category = "decor", Amount = 1, Kind = RewardKind.InventoryItem }
                },
                Limit = new ShopLotLimitData { Mode = mode, MaxPurchases = max }
            };

        private sealed class Harness
        {
            public ShopService Svc;
            public FakeSaveService Save;
            public FakeResourcesService Resources;
            public FakeRewardGrantService Rewards;
            public FakeConfigsService Configs;
            public FakeInventoryService Inventory;
            public SaveBackedShopRepository Repo;
        }

        private static Harness Build(
            IReadOnlyList<ShopConfig> lots,
            bool runAfterLoad = true,
            IShopRewardSpecProvider rewardSpecs = null)
        {
            var h = new Harness
            {
                Save = new FakeSaveService(),
                Resources = new FakeResourcesService(),
                Rewards = new FakeRewardGrantService(),
                Configs = new FakeConfigsService(),
                Inventory = new FakeInventoryService()
            };
            h.Configs.Seed(lots ?? new List<ShopConfig>());
            h.Repo = new SaveBackedShopRepository(h.Save);
            h.Svc = new ShopService(
                h.Save,
                h.Repo,
                h.Resources,
                h.Rewards,
                rewardSpecs ?? new ShopConfigRewardSpecProvider(h.Configs),
                h.Configs,
                h.Inventory);
            if (runAfterLoad)
                h.Svc.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            return h;
        }

        [Test]
        public void Constructor_SelfRegistersAsSaveHook()
        {
            var h = Build(new List<ShopConfig>(), runAfterLoad: false);
            CollectionAssert.Contains(h.Save.RegisteredHooks, h.Svc);
        }

        [Test]
        public void AfterLoadAsync_LoadsLotsFromConfigs()
        {
            var h = Build(new[] { DecorLot("lot_a", 10), DecorLot("lot_b", 20) });

            var lots = h.Svc.GetLots(Storefront);
            Assert.AreEqual(2, lots.Count);
            Assert.IsTrue(h.Svc.TryGetLot("lot_a", out var lotA));
            Assert.AreEqual(10, lotA.Price.Amount);
            Assert.AreEqual("reward_lot_a", lotA.RewardId);
        }

        [Test]
        public void AfterLoadAsync_NewspaperBooks_LoadsFourLotsWithDisplayTextInConfigOrder()
        {
            var lots = new[]
            {
                new ShopConfig
                {
                    Id = "newspaper_book_common_15",
                    StorefrontId = NewspaperShopLotIds.StorefrontBooks,
                    DisplayName = "Young Adult Favorites",
                    Description = "15 mixed books",
                    Price = new ShopPriceData { Currency = Gold, Amount = 20 },
                    RewardId = "book_box_common_15",
                    RewardItems = new RewardItemData[0],
                    Limit = new ShopLotLimitData { Mode = ShopLimitMode.Unlimited }
                },
                new ShopConfig
                {
                    Id = "newspaper_book_rare_8",
                    StorefrontId = NewspaperShopLotIds.StorefrontBooks,
                    DisplayName = "Fantasy Treasures",
                    Description = "8 rare books",
                    Price = new ShopPriceData { Currency = Gold, Amount = 30 },
                    RewardId = "book_box_rare_8",
                    RewardItems = new RewardItemData[0],
                    Limit = new ShopLotLimitData { Mode = ShopLimitMode.Unlimited }
                },
                new ShopConfig
                {
                    Id = "newspaper_book_genre_dystopic_1",
                    StorefrontId = NewspaperShopLotIds.StorefrontBooks,
                    DisplayName = "World Explorers",
                    Description = "1 dark fantasy pick",
                    Price = new ShopPriceData { Currency = Gold, Amount = 40 },
                    RewardId = "book_box_genre_dystopic_1",
                    RewardItems = new RewardItemData[0],
                    Limit = new ShopLotLimitData { Mode = ShopLimitMode.Unlimited }
                },
                new ShopConfig
                {
                    Id = NewspaperShopLotIds.BookBoxGenreHeartfelt1,
                    StorefrontId = NewspaperShopLotIds.StorefrontBooks,
                    DisplayName = "Heartfelt Stories",
                    Description = "1 romantic drama pick",
                    Price = new ShopPriceData { Currency = Gold, Amount = 40 },
                    RewardId = "book_box_genre_heartfelt_1",
                    RewardItems = new RewardItemData[0],
                    Limit = new ShopLotLimitData { Mode = ShopLimitMode.Unlimited }
                },
            };

            var h = Build(lots);

            var books = h.Svc.GetLots(NewspaperShopLotIds.StorefrontBooks);

            Assert.AreEqual(4, books.Count);
            Assert.AreEqual("newspaper_book_common_15", books[0].LotId);
            Assert.AreEqual("newspaper_book_rare_8", books[1].LotId);
            Assert.AreEqual("newspaper_book_genre_dystopic_1", books[2].LotId);
            Assert.AreEqual(NewspaperShopLotIds.BookBoxGenreHeartfelt1, books[3].LotId);
            Assert.AreEqual("Young Adult Favorites", books[0].DisplayName);
            Assert.AreEqual("15 mixed books", books[0].Description);
            Assert.AreEqual("Heartfelt Stories", books[3].DisplayName);
            Assert.AreEqual("1 romantic drama pick", books[3].Description);
        }

        [Test]
        public void AfterLoadAsync_NullDtoFromSave_TreatedAsEmpty()
        {
            // FakeSaveService returns null for any unseeded module — exercise that path explicitly.
            var h = Build(new[] { DecorLot("lot_a", 10) });

            Assert.AreEqual(0, h.Svc.GetPurchaseCount("lot_a"));
            Assert.IsTrue(h.Svc.IsAvailable("lot_a"));
        }

        [Test]
        public void Buy_Success_ChargesGoldAndGrants()
        {
            var h = Build(new[] { DecorLot("lot_a", 50) });
            h.Resources.Seed(Gold, 100);

            var result = h.Svc.BuyAsync("lot_a", CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(ShopPurchaseStatus.Success, result.Status);
            Assert.AreEqual(50, h.Resources.GetAmount(Gold));
            Assert.AreEqual(1, h.Resources.RemoveCalls.Count);
            Assert.AreEqual((Gold, 50, "shop:lot_a"), h.Resources.RemoveCalls[0]);
            Assert.AreEqual(1, h.Rewards.GrantCalls.Count);
            Assert.AreEqual("reward_lot_a", h.Rewards.GrantCalls[0].spec.Id);
            Assert.AreEqual("shop:lot_a", h.Rewards.GrantCalls[0].source);
        }

        [Test]
        public void Buy_NotEnoughGold_ReturnsFailure_NoCharge()
        {
            var h = Build(new[] { DecorLot("lot_a", 50) });
            h.Resources.Seed(Gold, 20);

            var result = h.Svc.BuyAsync("lot_a", CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(ShopPurchaseStatus.NotEnoughCurrency, result.Status);
            Assert.AreEqual(20, h.Resources.GetAmount(Gold));
            Assert.AreEqual(0, h.Resources.RemoveCalls.Count);
            Assert.AreEqual(0, h.Rewards.GrantCalls.Count);
        }

        [Test]
        public void Buy_MissingRewardSpec_ReturnsInternalError_NoCharge()
        {
            var h = Build(new[] { DecorLot("lot_a", 50) }, rewardSpecs: new MissingRewardSpecProvider());
            h.Resources.Seed(Gold, 100);

            LogAssert.Expect(LogType.Error, "[Shop] Missing RewardSpec for lot 'lot_a' (rewardId='reward_lot_a'). Purchase was blocked before charging currency.");

            var result = h.Svc.BuyAsync("lot_a", CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(ShopPurchaseStatus.InternalError, result.Status);
            Assert.AreEqual(100, h.Resources.GetAmount(Gold));
            Assert.AreEqual(0, h.Resources.RemoveCalls.Count);
            Assert.AreEqual(0, h.Rewards.GrantCalls.Count);
        }

        [Test]
        public void Buy_DisposableTwice_SecondReturnsLimit()
        {
            var h = Build(new[] { DecorLot("lot_a", 10, ShopLimitMode.Disposable, max: 1) });
            h.Resources.Seed(Gold, 100);

            var first = h.Svc.BuyAsync("lot_a", CancellationToken.None).GetAwaiter().GetResult();
            var second = h.Svc.BuyAsync("lot_a", CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(ShopPurchaseStatus.Success, first.Status);
            Assert.AreEqual(ShopPurchaseStatus.LimitReached, second.Status);
            Assert.AreEqual(1, h.Svc.GetPurchaseCount("lot_a"));
            Assert.IsFalse(h.Svc.IsAvailable("lot_a"));
            Assert.AreEqual(90, h.Resources.GetAmount(Gold), "Second buy must not charge.");
        }

        [Test]
        public void Buy_LotNotFound_ReturnsLotNotFound()
        {
            var h = Build(new List<ShopConfig>());
            h.Resources.Seed(Gold, 100);

            var result = h.Svc.BuyAsync("nonexistent", CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(ShopPurchaseStatus.LotNotFound, result.Status);
            Assert.AreEqual(100, h.Resources.GetAmount(Gold));
            Assert.AreEqual(0, h.Rewards.GrantCalls.Count);
        }

        [Test]
        public void Buy_PersistsPurchaseCountToRepo()
        {
            var h = Build(new[] { DecorLot("lot_a", 10) });
            h.Resources.Seed(Gold, 100);

            h.Svc.BuyAsync("lot_a", CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(h.Save.Store.ContainsKey(ShopSaveKeys.State));
            // Round-trip the persisted JSON to verify the counter is in there.
            var dto = h.Repo.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsTrue(dto.Lots.ContainsKey("lot_a"));
            Assert.AreEqual(1, dto.Lots["lot_a"].Purchases);
        }

        [Test]
        public void Buy_AlreadyOwnedInlineItem_ReturnsAlreadyOwned_NoCharge()
        {
            // Lot grants `vintage_globe` decor. Player already owns it → block before charging gold.
            var lot = new ShopConfig
            {
                Id = "lot_dupe", StorefrontId = Storefront,
                Price = new ShopPriceData { Currency = Gold, Amount = 30 },
                RewardId = "reward_dupe",
                RewardItems = new[]
                {
                    new RewardItemData { Id = "vintage_globe", Category = "decor", Amount = 1, Kind = RewardKind.InventoryItem }
                },
                Limit = new ShopLotLimitData { Mode = ShopLimitMode.Unlimited }
            };
            var h = Build(new[] { lot });
            h.Resources.Seed(Gold, 100);
            h.Inventory.Seed("vintage_globe", "decor");   // player already owns it

            var result = h.Svc.BuyAsync("lot_dupe", CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(ShopPurchaseStatus.AlreadyOwned, result.Status);
            Assert.AreEqual(100, h.Resources.GetAmount(Gold), "Gold must not be charged for already-owned items.");
            Assert.AreEqual(0, h.Rewards.GrantCalls.Count, "Grant must not be invoked.");
            Assert.IsFalse(h.Svc.IsAvailable("lot_dupe"), "Lot must read as unavailable for UI button state.");
        }

        [Test]
        public void Buy_InlineItemNotOwned_ProceedsNormally()
        {
            // Same shape as above but inventory is empty — happy path still works.
            var lot = new ShopConfig
            {
                Id = "lot_fresh", StorefrontId = Storefront,
                Price = new ShopPriceData { Currency = Gold, Amount = 30 },
                RewardId = "reward_fresh",
                RewardItems = new[]
                {
                    new RewardItemData { Id = "houseplant", Category = "decor", Amount = 1, Kind = RewardKind.InventoryItem }
                },
                Limit = new ShopLotLimitData { Mode = ShopLimitMode.Unlimited }
            };
            var h = Build(new[] { lot });
            h.Resources.Seed(Gold, 100);

            var result = h.Svc.BuyAsync("lot_fresh", CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(ShopPurchaseStatus.Success, result.Status);
            Assert.AreEqual(70, h.Resources.GetAmount(Gold));
            Assert.AreEqual(1, h.Rewards.GrantCalls.Count);
        }

        [Test]
        public void Buy_EmptyRewardItems_NotBlockedByInventoryCheck()
        {
            // Book-box style: empty rewardItems (expander fills at grant time). Inventory of the same
            // RewardId is irrelevant — owned-filter lives inside BookBoxRewardExpander, not here.
            var lot = new ShopConfig
            {
                Id = "lot_box", StorefrontId = Storefront,
                Price = new ShopPriceData { Currency = Gold, Amount = 20 },
                RewardId = "book_box_common_15",
                RewardItems = new RewardItemData[0],
                Limit = new ShopLotLimitData { Mode = ShopLimitMode.Unlimited }
            };
            var h = Build(new[] { lot });
            h.Resources.Seed(Gold, 100);
            h.Inventory.Seed("book_005", "book");    // already has a book, but lot has empty items

            var result = h.Svc.BuyAsync("lot_box", CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(ShopPurchaseStatus.Success, result.Status);
            Assert.IsTrue(h.Svc.IsAvailable("lot_box"));
        }

        [Test]
        public void AfterLoadAsync_MultipleStorefronts_PartitionedByStorefrontId()
        {
            // Mixed storefronts in one config — GetLots(X) must return only lots tagged with X.
            var lots = new[]
            {
                new ShopConfig
                {
                    Id = "lot_a", StorefrontId = "store.alpha",
                    Price = new ShopPriceData { Currency = Gold, Amount = 10 },
                    RewardId = "reward_a",
                    RewardItems = new RewardItemData[0],
                    Limit = new ShopLotLimitData { Mode = ShopLimitMode.Unlimited }
                },
                new ShopConfig
                {
                    Id = "lot_b", StorefrontId = "store.beta",
                    Price = new ShopPriceData { Currency = Gold, Amount = 20 },
                    RewardId = "reward_b",
                    RewardItems = new RewardItemData[0],
                    Limit = new ShopLotLimitData { Mode = ShopLimitMode.Unlimited }
                },
                new ShopConfig
                {
                    Id = "lot_c", StorefrontId = "store.alpha",
                    Price = new ShopPriceData { Currency = Gold, Amount = 30 },
                    RewardId = "reward_c",
                    RewardItems = new RewardItemData[0],
                    Limit = new ShopLotLimitData { Mode = ShopLimitMode.Unlimited }
                },
            };

            var h = Build(lots);

            var alpha = h.Svc.GetLots("store.alpha");
            var beta = h.Svc.GetLots("store.beta");
            var gamma = h.Svc.GetLots("store.unknown");

            Assert.AreEqual(2, alpha.Count);
            Assert.IsTrue(alpha.Any(l => l.LotId == "lot_a"));
            Assert.IsTrue(alpha.Any(l => l.LotId == "lot_c"));
            Assert.AreEqual(1, beta.Count);
            Assert.AreEqual("lot_b", beta[0].LotId);
            Assert.AreEqual(0, gamma.Count, "Unknown storefront returns empty list, not null.");
        }

        [Test]
        public void LotPurchased_Event_FiresWithGrantedSpec()
        {
            var h = Build(new[] { DecorLot("lot_a", 10) });
            h.Resources.Seed(Gold, 100);

            var expandedSpec = new RewardSpec("expanded", new[] { RewardItem.Resource(Gold, 1) });
            h.Rewards.OverrideGranted = expandedSpec;

            ShopPurchaseEvent? captured = null;
            h.Svc.LotPurchased += e => captured = e;

            h.Svc.BuyAsync("lot_a", CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(captured.HasValue);
            Assert.AreEqual("lot_a", captured.Value.Lot.LotId);
            Assert.AreSame(expandedSpec, captured.Value.Granted);
        }

        private sealed class MissingRewardSpecProvider : IShopRewardSpecProvider
        {
            public bool TryBuild(ShopLot lot, out RewardSpec spec)
            {
                spec = null;
                return false;
            }
        }
    }
}
