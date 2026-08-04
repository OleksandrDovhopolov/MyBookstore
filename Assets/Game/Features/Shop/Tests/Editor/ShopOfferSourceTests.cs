using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using Game.Rewards.API;
using Game.Shop.API;
using Game.Shop.UI;
using NUnit.Framework;

namespace Game.Shop.Tests.Editor
{
    public sealed class ShopOfferSourceTests
    {
        [Test]
        public void GetOffers_FiltersByStorefrontAndFormatsStateAndPrice()
        {
            var lots = new[]
            {
                new ShopLot(
                    "book_a",
                    NewspaperShopLotIds.StorefrontBooks,
                    new ShopPrice("gold", 30),
                    "book_box_common_15",
                    ShopLotLimit.Unlimited(),
                    "Book A",
                    "Book description"),
                new ShopLot(
                    "decor_free",
                    NewspaperShopLotIds.StorefrontDecor,
                    new ShopPrice("gold", 0),
                    "decor_vintage_globe",
                    ShopLotLimit.Disposable(1),
                    "Vintage Globe",
                    "Free decor"),
                new ShopLot(
                    "decor_sold",
                    NewspaperShopLotIds.StorefrontDecor,
                    new ShopPrice("gold", 50),
                    "decor_coffee_pot",
                    ShopLotLimit.Disposable(1),
                    "Coffee Machine",
                    "Paid decor"),
                new ShopLot(
                    "fuel_lot",
                    NewspaperShopLotIds.StorefrontConsumables,
                    new ShopPrice("gold", 20),
                    "consumable_fuel_canister",
                    ShopLotLimit.Unlimited(),
                    "Fuel Canister",
                    "Useful supply"),
            };
            var shop = new FakeShopService(lots, unavailableLotId: "decor_sold");
            var configs = new FakeConfigsService(new Dictionary<string, RewardItemData>
            {
                ["decor_free"] = RewardItem("vintage_globe", "decor"),
                ["decor_sold"] = RewardItem("coffee_pot", "decor"),
                ["fuel_lot"] = RewardItem("fuel_canister", "consumable"),
            });
            var source = new ShopOfferSource(shop, configs);

            var books = source.GetBookOffers();
            var decor = source.GetDecorOffers();
            var consumables = source.GetConsumableOffers();

            Assert.AreEqual(1, books.Count);
            Assert.AreEqual("book_a", books[0].LotId);
            Assert.AreEqual("book_box", books[0].IconId);
            Assert.AreEqual("Book A", books[0].DisplayName);
            Assert.AreEqual("30", books[0].PriceText);
            Assert.AreEqual("NEW!", books[0].StateText);
            Assert.IsTrue(books[0].IsAvailable);
            Assert.IsFalse(books[0].IsDecor);

            Assert.AreEqual(2, decor.Count);
            // Icon id comes from decors.json (the lot's decor reward item id), not the shop lot id.
            Assert.AreEqual("vintage_globe", decor[0].IconId);
            Assert.AreEqual("FREE", decor[0].PriceText);
            Assert.AreEqual("NEW!", decor[0].StateText);
            Assert.IsTrue(decor[0].IsAvailable);
            Assert.IsTrue(decor[0].IsDecor);
            Assert.AreEqual("coffee_pot", decor[1].IconId);
            Assert.AreEqual("50", decor[1].PriceText);
            Assert.AreEqual("SOLD", decor[1].StateText);
            Assert.IsFalse(decor[1].IsAvailable);
            Assert.IsTrue(decor[1].IsDecor);

            Assert.AreEqual(1, consumables.Count);
            Assert.AreEqual("fuel_lot", consumables[0].LotId);
            Assert.AreEqual("fuel_canister", consumables[0].IconId);
            Assert.AreEqual("20", consumables[0].PriceText);
            Assert.IsTrue(consumables[0].IsAvailable);
            Assert.IsFalse(consumables[0].IsDecor);
        }

        [Test]
        public void GetConsumableOffers_UsesInventoryRewardIcon_WhenRewardCategoryDiffersFromStorefront()
        {
            var lot = new ShopLot(
                "newspaper_quest_item_map",
                NewspaperShopLotIds.StorefrontConsumables,
                new ShopPrice("gold", 200),
                "quest_item_map",
                ShopLotLimit.Disposable(1),
                "Map",
                "Route to the village");
            var shop = new FakeShopService(new[] { lot }, unavailableLotId: null);
            var configs = new FakeConfigsService(new Dictionary<string, RewardItemData>
            {
                ["newspaper_quest_item_map"] = RewardItem("map", InventoryCategories.QuestItem),
            });
            var source = new ShopOfferSource(shop, configs);

            var offers = source.GetConsumableOffers();

            Assert.AreEqual(1, offers.Count);
            Assert.AreEqual("map", offers[0].IconId);
        }

        private static RewardItemData RewardItem(string id, string category) =>
            new RewardItemData
            {
                Id = id,
                Category = category,
                Amount = 1,
                Kind = RewardKind.InventoryItem,
            };

        private sealed class FakeShopService : IShopService
        {
            private readonly List<ShopLot> _lots;
            private readonly string _unavailableLotId;

            public FakeShopService(IEnumerable<ShopLot> lots, string unavailableLotId)
            {
                _lots = new List<ShopLot>(lots);
                _unavailableLotId = unavailableLotId;
            }

            public IReadOnlyList<ShopLot> GetLots(string storefrontId)
            {
                var result = new List<ShopLot>();
                for (var i = 0; i < _lots.Count; i++)
                {
                    var lot = _lots[i];
                    if (string.Equals(lot.StorefrontId, storefrontId, StringComparison.Ordinal))
                        result.Add(lot);
                }

                return result;
            }

            public IReadOnlyList<ShopLot> GetOfferedLots(string storefrontId) => GetLots(storefrontId);

            public bool TryGetLot(string lotId, out ShopLot lot)
            {
                for (var i = 0; i < _lots.Count; i++)
                {
                    if (!string.Equals(_lots[i].LotId, lotId, StringComparison.Ordinal)) continue;
                    lot = _lots[i];
                    return true;
                }

                lot = null;
                return false;
            }

            public int GetPurchaseCount(string lotId) => 0;

            public bool IsAvailable(string lotId) =>
                !string.Equals(lotId, _unavailableLotId, StringComparison.Ordinal);

            public UniTask<ShopPurchaseResult> BuyAsync(string lotId, CancellationToken ct) =>
                UniTask.FromResult(ShopPurchaseResult.Fail(ShopPurchaseStatus.InternalError));

            public event Action<ShopPurchaseEvent> LotPurchased;
        }

        // Returns a ShopConfig (keyed by lot id) whose single decor reward item carries the decors.json id.
        private sealed class FakeConfigsService : IConfigsService
        {
            private readonly Dictionary<string, ShopConfig> _shopConfigs;

            public FakeConfigsService(IReadOnlyDictionary<string, RewardItemData> rewardByLotId)
            {
                _shopConfigs = rewardByLotId.ToDictionary(
                    pair => pair.Key,
                    pair => new ShopConfig
                    {
                        Id = pair.Key,
                        RewardItems = new[]
                        {
                            pair.Value,
                        },
                    },
                    StringComparer.Ordinal);
            }

            public UniTask WarmupAsync(CancellationToken ct) => UniTask.CompletedTask;

            public IReadOnlyList<T> GetAll<T>() where T : class, IConfig => Array.Empty<T>();

            public T Get<T>(string id) where T : class, IConfig
            {
                TryGet<T>(id, out var config);
                return config;
            }

            public bool TryGet<T>(string id, out T config) where T : class, IConfig
            {
                if (typeof(T) == typeof(ShopConfig)
                    && !string.IsNullOrEmpty(id)
                    && _shopConfigs.TryGetValue(id, out var cfg))
                {
                    config = cfg as T;
                    return true;
                }

                config = null;
                return false;
            }

            public UniTask<T> GetAsync<T>(string id) where T : class, IConfig =>
                UniTask.FromResult(Get<T>(id));

            public bool IsExists<T>(string id) where T : class, IConfig => TryGet<T>(id, out _);
        }
    }
}
