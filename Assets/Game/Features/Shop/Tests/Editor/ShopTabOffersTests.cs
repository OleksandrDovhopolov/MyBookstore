using System.Collections.Generic;
using System.Linq;
using Game.Shop.UI;
using NUnit.Framework;

namespace Game.Shop.Tests.Editor
{
    public sealed class ShopTabOffersTests
    {
        [Test]
        public void Build_Boxes_ReturnsBookOffersOnly()
        {
            var source = new FakeShopOfferSource(
                books: new[] { Offer("box_a"), Offer("box_b") },
                decor: new[] { Offer("decor_a", isDecor: true) },
                consumables: new[] { Offer("fuel") });

            var offers = ShopTabOffers.Build(source, ShopTab.Boxes);

            CollectionAssert.AreEqual(new[] { "box_a", "box_b" }, LotIds(offers));
        }

        [Test]
        public void Build_Decor_MovesUnavailableDecorLast()
        {
            var source = new FakeShopOfferSource(
                books: null,
                decor: new[]
                {
                    Offer("decor_sold", isAvailable: false, isDecor: true),
                    Offer("decor_available", isDecor: true),
                    Offer("decor_sold_b", isAvailable: false, isDecor: true),
                },
                consumables: null);

            var offers = ShopTabOffers.Build(source, ShopTab.Decor);

            CollectionAssert.AreEqual(
                new[] { "decor_available", "decor_sold", "decor_sold_b" },
                LotIds(offers));
        }

        [Test]
        public void Build_Consumable_ReturnsConsumableStorefrontIncludingQuestItems()
        {
            var source = new FakeShopOfferSource(
                books: new[] { Offer("box_a") },
                decor: new[] { Offer("decor_a", isDecor: true) },
                consumables: new[] { Offer("newspaper_quest_item_map"), Offer("fuel") });

            var offers = ShopTabOffers.Build(source, ShopTab.Consumable);

            CollectionAssert.AreEqual(new[] { "newspaper_quest_item_map", "fuel" }, LotIds(offers));
        }

        [Test]
        public void Build_All_PreservesBookConsumableDecorOrder()
        {
            var source = new FakeShopOfferSource(
                books: new[] { Offer("box_a"), Offer("box_b") },
                decor: new[]
                {
                    Offer("decor_sold", isAvailable: false, isDecor: true),
                    Offer("decor_available", isDecor: true),
                },
                consumables: new[] { Offer("newspaper_quest_item_map"), Offer("fuel") });

            var offers = ShopTabOffers.Build(source, ShopTab.All);

            CollectionAssert.AreEqual(
                new[] { "box_a", "box_b", "newspaper_quest_item_map", "fuel", "decor_available", "decor_sold" },
                LotIds(offers));
        }

        [Test]
        public void Build_ReturnsEmpty_WhenSourceOrListsAreNull()
        {
            Assert.IsEmpty(ShopTabOffers.Build(null, ShopTab.All));

            var source = new FakeShopOfferSource(books: null, decor: null, consumables: null);

            Assert.IsEmpty(ShopTabOffers.Build(source, ShopTab.All));
            Assert.IsEmpty(ShopTabOffers.Build(source, ShopTab.Boxes));
            Assert.IsEmpty(ShopTabOffers.Build(source, ShopTab.Decor));
            Assert.IsEmpty(ShopTabOffers.Build(source, ShopTab.Consumable));
        }

        private static IEnumerable<string> LotIds(IReadOnlyList<ShopOffer> offers) =>
            offers.Select(offer => offer.LotId);

        private static ShopOffer Offer(
            string lotId,
            bool isAvailable = true,
            bool isDecor = false) =>
            new(
                lotId,
                iconId: lotId,
                displayName: lotId,
                description: string.Empty,
                priceText: "1",
                isAvailable,
                stateText: string.Empty,
                isDecor);

        private sealed class FakeShopOfferSource : IShopOfferSource
        {
            private readonly IReadOnlyList<ShopOffer> _books;
            private readonly IReadOnlyList<ShopOffer> _decor;
            private readonly IReadOnlyList<ShopOffer> _consumables;

            public FakeShopOfferSource(
                IReadOnlyList<ShopOffer> books,
                IReadOnlyList<ShopOffer> decor,
                IReadOnlyList<ShopOffer> consumables)
            {
                _books = books;
                _decor = decor;
                _consumables = consumables;
            }

            public IReadOnlyList<ShopOffer> GetBookOffers() => _books;

            public IReadOnlyList<ShopOffer> GetDecorOffers() => _decor;

            public IReadOnlyList<ShopOffer> GetConsumableOffers() => _consumables;
        }
    }
}
