using Book.Sell.API;
using Book.Sell.Services;
using Game.Configs.Models;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor
{
    public sealed class RecommendationScoringServiceTests
    {
        private static BookConfig SciFiBook() => new()
        {
            Id = "book_sci",
            Title = "Sci",
            Genres = new[] { "sci-fi" },
            Qualities = new[] { "space", "survival", "engineering" }
        };

        private static RequestConfig SciFiRequest(int maxPrice = 90) => new()
        {
            Id = "req_sci",
            Text = "...",
            DesiredGenres = new[] { "sci-fi" },
            DesiredQualities = new[] { "space", "survival", "engineering" },
            MaxPrice = maxPrice,
            BaseRewardGold = 25
        };

        private static LocationConfig University() => new()
        {
            Id = "loc_uni",
            DemandGenres = new[] { "sci-fi" },
            DemandQualities = new[] { "study" }
        };

        private static RecommendationScoringService Sut() => new();

        [Test]
        public void ExactMatch_Returns_Excellent_WithFullBreakdown()
        {
            var result = Sut().Score(SciFiBook(), SciFiRequest(), University());

            // genre +3, qualities 3*2=6, price +1, location +1 = 11
            Assert.AreEqual(RecommendationTier.Excellent, result.Tier);
            Assert.AreEqual(3, result.Breakdown.GenrePoints);
            Assert.AreEqual(6, result.Breakdown.QualityPoints);
            Assert.AreEqual(1, result.Breakdown.PricePoints);
            Assert.AreEqual(1, result.Breakdown.LocationPoints);
            Assert.AreEqual(11, result.Breakdown.Total);
            Assert.AreEqual(BookConfig.FixedPriceGold + 25, result.GoldEarned, "Excellent = fixed book price + BaseRewardGold.");
        }

        [Test]
        public void GenreMismatch_NoQualities_Returns_Failed_ZeroGold()
        {
            var book = new BookConfig { Id = "b1", Genres = new[] { "romance" } };
            var req = new RequestConfig { Id = "r1", DesiredGenres = new[] { "sci-fi" }, MaxPrice = 100 };
            var result = Sut().Score(book, req, null);

            // only price +1 = 1
            Assert.AreEqual(RecommendationTier.Failed, result.Tier);
            Assert.AreEqual(0, result.GoldEarned);
        }

        [Test]
        public void MaxPriceZero_SkipsPriceScoring()
        {
            var req = SciFiRequest(maxPrice: 0);
            var result = Sut().Score(SciFiBook(), req, null);

            Assert.AreEqual(0, result.Breakdown.PricePoints, "MaxPrice<=0 -> price scoring is skipped.");
            Assert.IsFalse(result.Reason.PriceFits);
        }

        [Test]
        public void Price_OverBudget_NoPricePoints_ButGenreQualitiesStillCount()
        {
            var req = SciFiRequest(maxPrice: 5);
            var result = Sut().Score(SciFiBook(), req, null);

            Assert.AreEqual(0, result.Breakdown.PricePoints, "Fixed price is 10, so a max price below 10 does not fit.");
            Assert.Greater(result.Breakdown.Total, 3, "Genre + qualities still produce a lot.");
        }

        [Test]
        public void LocationBonus_Caps_AtOne_EvenWithGenreAndTagMatch()
        {
            // Both Genre and Qualities match the location — bonus stays at +1.
            var result = Sut().Score(SciFiBook(), SciFiRequest(), University());
            Assert.AreEqual(1, result.Breakdown.LocationPoints);
            Assert.IsTrue(result.Reason.LocationBonus);
        }

        [Test]
        public void NormalTier_3to5_Returns_Normal_AndGoldIsFixedPrice()
        {
            // genre +3 = 3 -> Normal
            var book = new BookConfig { Id = "b1", Genres = new[] { "sci-fi" } };
            var req = new RequestConfig { Id = "r1", DesiredGenres = new[] { "sci-fi" }, MaxPrice = 0 };
            var result = Sut().Score(book, req, null);

            Assert.AreEqual(RecommendationTier.Normal, result.Tier);
            Assert.AreEqual(BookConfig.FixedPriceGold, result.GoldEarned);
        }

        [Test]
        public void NullLocation_DoesNotCrash_NoLocationBonus()
        {
            var result = Sut().Score(SciFiBook(), SciFiRequest(), null);
            Assert.AreEqual(0, result.Breakdown.LocationPoints);
            Assert.IsFalse(result.Reason.LocationBonus);
        }

        [Test]
        public void MatchedQualities_AreReported_InReason()
        {
            var result = Sut().Score(SciFiBook(), SciFiRequest(), null);
            CollectionAssert.AreEquivalent(new[] { "space", "survival", "engineering" }, result.Reason.MatchedQualities);
        }

        [Test]
        public void CaseInsensitive_GenreMatch()
        {
            var book = new BookConfig { Id = "b", Genres = new[] { "SCI-FI" } };
            var req = new RequestConfig { Id = "r", DesiredGenres = new[] { "sci-fi" } };
            var result = Sut().Score(book, req, null);
            Assert.AreEqual(3, result.Breakdown.GenrePoints);
        }
    }
}
