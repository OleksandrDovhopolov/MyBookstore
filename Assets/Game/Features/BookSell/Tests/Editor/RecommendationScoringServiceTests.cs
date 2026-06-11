using Book.Sell.Domain;
using Book.Sell.Services;
using Game.Configs.Models;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor
{
    public sealed class RecommendationScoringServiceTests
    {
        private static BookConfig SciFiBook(int price = 80) => new()
        {
            Id = "book_sci",
            Title = "Sci",
            Genre = "sci-fi",
            BasePrice = price,
            Tags = new[] { "space", "survival", "engineering" },
            Mood = new[] { "smart", "tense", "optimistic" }
        };

        private static RequestConfig SciFiRequest(int maxPrice = 90) => new()
        {
            Id = "req_sci",
            Text = "...",
            DesiredGenres = new[] { "sci-fi" },
            DesiredTags = new[] { "space", "survival", "engineering" },
            DesiredMood = new[] { "smart", "tense", "optimistic" },
            MaxPrice = maxPrice,
            BaseRewardGold = 25
        };

        private static LocationConfig University() => new()
        {
            Id = "loc_uni",
            DemandGenres = new[] { "sci-fi" },
            DemandTags = new[] { "study" }
        };

        private static RecommendationScoringService Sut() => new();

        [Test]
        public void ExactMatch_Returns_Excellent_WithFullBreakdown()
        {
            var result = Sut().Score(SciFiBook(), SciFiRequest(), University());

            // genre +3, tag 3×2=6, mood 3×1=3, price +1, location +1 = 14
            Assert.AreEqual(RecommendationTier.Excellent, result.Tier);
            Assert.AreEqual(3, result.Breakdown.GenrePoints);
            Assert.AreEqual(6, result.Breakdown.TagPoints);
            Assert.AreEqual(3, result.Breakdown.MoodPoints);
            Assert.AreEqual(1, result.Breakdown.PricePoints);
            Assert.AreEqual(1, result.Breakdown.LocationPoints);
            Assert.AreEqual(14, result.Breakdown.Total);
            Assert.AreEqual(80 + 25, result.GoldEarned, "Excellent = BasePrice + BaseRewardGold.");
        }

        [Test]
        public void GenreMismatch_NoTagsNoMood_Returns_Failed_ZeroGold()
        {
            var book = new BookConfig { Id = "b1", Genre = "romance", BasePrice = 50 };
            var req = new RequestConfig { Id = "r1", DesiredGenres = new[] { "sci-fi" }, MaxPrice = 100 };
            var result = Sut().Score(book, req, null);

            // только price +1 = 1
            Assert.AreEqual(RecommendationTier.Failed, result.Tier);
            Assert.AreEqual(0, result.GoldEarned);
        }

        [Test]
        public void MaxPriceZero_SkipsPriceScoring()
        {
            var req = SciFiRequest(maxPrice: 0);
            var result = Sut().Score(SciFiBook(), req, null);

            Assert.AreEqual(0, result.Breakdown.PricePoints, "MaxPrice<=0 → price scoring пропущен.");
            Assert.IsFalse(result.Reason.PriceFits);
        }

        [Test]
        public void Price_OverBudget_NoPricePoints_ButGenreTagsStillCount()
        {
            var book = SciFiBook(price: 200);
            var req = SciFiRequest(maxPrice: 90);
            var result = Sut().Score(book, req, null);

            Assert.AreEqual(0, result.Breakdown.PricePoints);
            Assert.Greater(result.Breakdown.Total, 3, "Жанр+теги+тон уже дают много.");
        }

        [Test]
        public void LocationBonus_Caps_AtOne_EvenWithGenreAndTagMatch()
        {
            // и Genre, и Tags матчат с локацией — бонус остаётся +1
            var result = Sut().Score(SciFiBook(), SciFiRequest(), University());
            Assert.AreEqual(1, result.Breakdown.LocationPoints);
            Assert.IsTrue(result.Reason.LocationBonus);
        }

        [Test]
        public void NormalTier_3to5_Returns_Normal_AndGoldIsBasePrice()
        {
            // только genre +3 = 3 → Normal
            var book = new BookConfig { Id = "b1", Genre = "sci-fi", BasePrice = 80 };
            var req = new RequestConfig { Id = "r1", DesiredGenres = new[] { "sci-fi" }, MaxPrice = 0 };
            var result = Sut().Score(book, req, null);

            Assert.AreEqual(RecommendationTier.Normal, result.Tier);
            Assert.AreEqual(80, result.GoldEarned);
        }

        [Test]
        public void NullLocation_DoesNotCrash_NoLocationBonus()
        {
            var result = Sut().Score(SciFiBook(), SciFiRequest(), null);
            Assert.AreEqual(0, result.Breakdown.LocationPoints);
            Assert.IsFalse(result.Reason.LocationBonus);
        }

        [Test]
        public void MatchedTags_AreReported_InReason()
        {
            var result = Sut().Score(SciFiBook(), SciFiRequest(), null);
            CollectionAssert.AreEquivalent(new[] { "space", "survival", "engineering" }, result.Reason.MatchedTags);
        }

        [Test]
        public void CaseInsensitive_GenreMatch()
        {
            var book = new BookConfig { Id = "b", Genre = "SCI-FI", BasePrice = 80 };
            var req = new RequestConfig { Id = "r", DesiredGenres = new[] { "sci-fi" } };
            var result = Sut().Score(book, req, null);
            Assert.AreEqual(3, result.Breakdown.GenrePoints);
        }
    }
}
