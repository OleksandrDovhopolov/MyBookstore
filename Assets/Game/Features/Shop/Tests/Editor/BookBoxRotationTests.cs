using System.Collections.Generic;
using System.Linq;
using Game.Rewards.Services;
using Game.Shop.API;
using Game.Shop.Services;
using NUnit.Framework;

namespace Game.Shop.Tests.Editor
{
    public sealed class BookBoxRotationTests
    {
        [Test]
        public void Select_SameDay_ReturnsSameLots()
        {
            var lots = RotationLots();

            var first = BookBoxRotation.Select(lots, 7);
            var second = BookBoxRotation.Select(lots.Reverse().ToArray(), 7);

            CollectionAssert.AreEqual(LotIds(first), LotIds(second));
        }

        [Test]
        public void Select_DifferentDays_ChangesLots()
        {
            var lots = RotationLots();

            var first = BookBoxRotation.Select(lots, 1);
            var second = BookBoxRotation.Select(lots, 2);

            Assert.IsFalse(LotIds(first).SequenceEqual(LotIds(second)));
        }

        [Test]
        public void Select_ReturnsTwoGeneralAndTwoGenre_WhenBucketsCanFill()
        {
            var selected = BookBoxRotation.Select(RotationLots(), 3);

            Assert.AreEqual(4, selected.Count);
            Assert.AreEqual(2, CountKind(selected, BookBoxKind.General));
            Assert.AreEqual(2, CountKind(selected, BookBoxKind.Genre));
        }

        [Test]
        public void Select_FillsFromOtherBucket_WhenOneBucketIsShort()
        {
            var lots = new[]
            {
                Lot("general_a", "book_box_general_5"),
                Lot("genre_a", "book_box_genre_classic_8"),
                Lot("genre_b", "book_box_genre_crime_8"),
                Lot("genre_c", "book_box_genre_drama_8"),
                Lot("genre_d", "book_box_genre_fact_8"),
            };

            var selected = BookBoxRotation.Select(lots, 5);

            Assert.AreEqual(4, selected.Count);
            Assert.AreEqual(1, CountKind(selected, BookBoxKind.General));
            Assert.AreEqual(3, CountKind(selected, BookBoxKind.Genre));
        }

        [Test]
        public void Select_IgnoresLotsWithoutRule()
        {
            var lots = new[]
            {
                Lot("general_a", "book_box_general_5"),
                Lot("general_b", "book_box_general_10"),
                Lot("unknown", "book_box_missing"),
                Lot("genre_a", "book_box_genre_classic_8"),
                Lot("genre_b", "book_box_genre_crime_8"),
            };

            var selected = BookBoxRotation.Select(lots, 9);

            Assert.AreEqual(4, selected.Count);
            Assert.IsFalse(LotIds(selected).Contains("unknown"));
        }

        private static IReadOnlyList<ShopLot> RotationLots() => new[]
        {
            Lot("general_5", "book_box_general_5"),
            Lot("general_10", "book_box_general_10"),
            Lot("common_15", "book_box_common_15"),
            Lot("rare_8", "book_box_rare_8"),
            Lot("classic", "book_box_genre_classic_8"),
            Lot("crime", "book_box_genre_crime_8"),
            Lot("drama", "book_box_genre_drama_8"),
            Lot("fact", "book_box_genre_fact_8"),
            Lot("fantasy", "book_box_genre_fantasy_8"),
            Lot("kids", "book_box_genre_kids_8"),
            Lot("travel", "book_box_genre_travel_8"),
        };

        private static ShopLot Lot(string lotId, string rewardId) =>
            new(
                lotId,
                NewspaperShopLotIds.StorefrontBooks,
                new ShopPrice("gold", 1),
                rewardId,
                ShopLotLimit.Daily(1),
                lotId,
                rewardId);

        private static IEnumerable<string> LotIds(IReadOnlyList<ShopLot> lots) =>
            lots.Select(lot => lot.LotId);

        private static int CountKind(IReadOnlyList<ShopLot> lots, BookBoxKind kind) =>
            lots.Count(lot => BookBoxPoolRules.TryGet(lot.RewardId, out var rule) && rule.Kind == kind);
    }
}
