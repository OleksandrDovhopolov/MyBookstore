using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Conditions.API;
using Game.Conditions.Services;
using Game.Shop.API;
using Game.Shop.Conditions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Shop.Tests.Editor
{
    /// <summary>
    /// "shopPurchases" sums the lifetime purchase counters of every lot in the listed storefronts.
    /// It reads GetLots (the whole catalog), not GetOfferedLots, so a lot that rotated out of the daily
    /// book-box selection or is no longer unlocked still counts toward the total.
    /// </summary>
    public sealed class ShopPurchasesConditionTests
    {
        private const string Books = "newspaper.books";
        private const string Decor = "newspaper.decor";

        [Test]
        public void SumsPurchasesAcrossLotsOfOneStorefront()
        {
            var shop = Shop();
            shop.Add(Books, "box_a", purchases: 2);
            shop.Add(Books, "box_b", purchases: 1);
            shop.Add(Decor, "globe", purchases: 5);

            var condition = Parse(shop, Node(3, Books));

            var result = condition.Evaluate();
            Assert.IsTrue(result.IsMet);
            Assert.AreEqual(3, result.Current, "decor purchases must not leak into the books total");
            Assert.AreEqual(3, result.Target);
        }

        [Test]
        public void NotMetBelowMin()
        {
            var shop = Shop();
            shop.Add(Books, "box_a", purchases: 2);

            var result = Parse(shop, Node(3, Books)).Evaluate();

            Assert.IsFalse(result.IsMet);
            Assert.AreEqual(2, result.Current);
        }

        [Test]
        public void SumsAcrossSeveralStorefronts()
        {
            var shop = Shop();
            shop.Add(Decor, "globe", purchases: 2);
            shop.Add("newspaper.consumables", "fuel", purchases: 1);

            Assert.IsTrue(Parse(shop, Node(3, Decor, "newspaper.consumables")).Evaluate().IsMet);
        }

        [Test]
        public void UnknownStorefrontContributesNothing()
        {
            var shop = Shop();
            shop.Add(Books, "box_a", purchases: 9);

            Assert.AreEqual(0, Parse(shop, Node(1, "nope")).Evaluate().Current);
        }

        [Test]
        public void MissingStorefrontIds_Throws()
        {
            var shop = Shop();
            var factory = new ShopPurchasesConditionFactory(() => shop);
            Assert.Throws<ArgumentException>(() => factory.Create(new JObject { ["min"] = 3 }));
        }

        [Test]
        public void MinBelowOne_IsClampedSoTheConditionIsNeverFreelyMet()
        {
            var shop = Shop();
            shop.Add(Books, "box_a", purchases: 0);

            var result = Parse(shop, Node(0, Books)).Evaluate();

            Assert.AreEqual(1, result.Target);
            Assert.IsFalse(result.IsMet);
        }

        [Test]
        public void ChangedFiresOnPurchase()
        {
            var shop = new FakeShopService();
            var factory = new ShopPurchasesConditionFactory(() => shop);

            var raised = 0;
            void Handler() => raised++;

            factory.Changed += Handler;
            shop.RaisePurchased();
            Assert.AreEqual(1, raised);

            factory.Changed -= Handler;
            shop.RaisePurchased();
            Assert.AreEqual(1, raised, "unsubscribed handlers must stop receiving purchases");
        }

        // ----- helpers -----

        private static FakeShopService Shop() => new FakeShopService();

        private static JObject Node(int min, params string[] storefronts)
            => new JObject
            {
                ["type"] = ShopPurchasesConditionFactory.TypeId,
                ["storefrontIds"] = new JArray(storefronts),
                ["min"] = min
            };

        private static ICondition Parse(FakeShopService shop, JObject node)
        {
            var registry = new ConditionFactoryRegistry(
                new IConditionFactory[] { new ShopPurchasesConditionFactory(() => shop) });
            return new ConditionParser(registry).Parse(node);
        }

        private sealed class FakeShopService : IShopService
        {
            private readonly Dictionary<string, List<ShopLot>> _byStorefront = new(StringComparer.Ordinal);
            private readonly Dictionary<string, int> _purchases = new(StringComparer.Ordinal);

            public void Add(string storefrontId, string lotId, int purchases)
            {
                if (!_byStorefront.TryGetValue(storefrontId, out var lots))
                {
                    lots = new List<ShopLot>();
                    _byStorefront[storefrontId] = lots;
                }

                lots.Add(new ShopLot(lotId, storefrontId, new ShopPrice("gold", 0), lotId,
                    ShopLotLimit.Unlimited()));
                _purchases[lotId] = purchases;
            }

            public void RaisePurchased() => LotPurchased?.Invoke(default);

            public IReadOnlyList<ShopLot> GetLots(string storefrontId)
                => _byStorefront.TryGetValue(storefrontId, out var lots) ? lots : Array.Empty<ShopLot>();

            public IReadOnlyList<ShopLot> GetOfferedLots(string storefrontId)
                => throw new InvalidOperationException("the condition must read the full catalog, not the offers");

            public bool TryGetLot(string lotId, out ShopLot lot)
            {
                lot = null;
                return false;
            }

            public int GetPurchaseCount(string lotId)
                => _purchases.TryGetValue(lotId, out var count) ? count : 0;

            public bool IsAvailable(string lotId) => false;

            public UniTask<ShopPurchaseResult> BuyAsync(string lotId, CancellationToken ct)
                => throw new NotSupportedException();

            public event Action<ShopPurchaseEvent> LotPurchased;
        }
    }
}
