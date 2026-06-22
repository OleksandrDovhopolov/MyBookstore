using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Newspaper.UI;
using Game.Shop.API;
using NUnit.Framework;

namespace Game.Shop.Tests.Editor
{
    public sealed class NewspaperOfferSourceTests
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
            };
            var shop = new FakeShopService(lots, unavailableLotId: "decor_sold");
            var source = new ShopBackedNewspaperOfferSource(shop);

            var books = source.GetBookOffers();
            var decor = source.GetDecorOffers();

            Assert.AreEqual(1, books.Count);
            Assert.AreEqual("book_a", books[0].LotId);
            Assert.AreEqual("Book A", books[0].DisplayName);
            Assert.AreEqual("30", books[0].PriceText);
            Assert.AreEqual("NEW!", books[0].StateText);
            Assert.IsTrue(books[0].IsAvailable);

            Assert.AreEqual(2, decor.Count);
            Assert.AreEqual("FREE", decor[0].PriceText);
            Assert.AreEqual("NEW!", decor[0].StateText);
            Assert.IsTrue(decor[0].IsAvailable);
            Assert.AreEqual("50", decor[1].PriceText);
            Assert.AreEqual("SOLD", decor[1].StateText);
            Assert.IsFalse(decor[1].IsAvailable);
        }

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
    }
}
