using Book.Sell.API;
using Game.DayCycle.Results.Services;
using NUnit.Framework;

namespace Game.DayCycle.Tests.Editor.Results
{
    public sealed class DefaultResultsRewardServiceTests
    {
        private static SalesDayResult SalesWith(int gold, int exc, int norm, int fail, int skip,
            params RecommendationResult[] recs)
        {
            var s = new SalesDayResult
            {
                Day = 1,
                GoldEarned = gold,
                ExcellentCount = exc, NormalCount = norm, FailedCount = fail, SkippedCount = skip
            };
            foreach (var r in recs) s.Recommendations.Add(r);
            return s;
        }

        private static RecommendationResult Rec(string id, RecommendationTier tier, int score)
            => new(id, $"book_{id}", tier,
                new ScoreBreakdown(score, 0, 0, 0, 0),
                RecommendationReason.Empty, 0);

        private static DefaultResultsRewardService Sut() => new();

        [Test]
        public void GoldDelta_EqualsSalesGoldEarned()
        {
            var result = Sut().Compute(SalesWith(123, 0, 0, 0, 0), currentReputation: 0);
            Assert.AreEqual(123, result.GoldDelta);
        }

        [Test]
        public void ReputationDelta_EqualsExcellentCount()
        {
            var result = Sut().Compute(SalesWith(0, exc: 3, 0, 0, 0), currentReputation: 0);
            Assert.AreEqual(3, result.ReputationDelta);
        }

        [Test]
        public void Reputation_PenaltyApplied_WhenFailedExceedsThreshold()
        {
            // failed > 2 → -1
            var result = Sut().Compute(SalesWith(0, exc: 2, norm: 0, fail: 3, skip: 0), currentReputation: 10);
            Assert.AreEqual(1, result.ReputationDelta, "2 excellent − 1 penalty = +1");
        }

        [Test]
        public void Reputation_NeverGoesBelowZero_AfterPenalty()
        {
            var result = Sut().Compute(SalesWith(0, exc: 0, norm: 0, fail: 5, skip: 0), currentReputation: 0);
            Assert.AreEqual(0, result.ReputationDelta, "Clamp delta so currentRep + delta >= 0.");
        }

        [Test]
        public void BestMatch_PicksHighestScore()
        {
            var sales = SalesWith(0, 0, 0, 0, 0,
                Rec("a", RecommendationTier.Normal, 4),
                Rec("b", RecommendationTier.Excellent, 9),
                Rec("c", RecommendationTier.Normal, 5));

            var best = Sut().Compute(sales, 0).BestMatch;
            Assert.IsNotNull(best);
            Assert.AreEqual("book_b", best.BookId);
            Assert.AreEqual(9, best.Score);
        }

        [Test]
        public void BestMatch_TieBreaker_PrefersHigherTier()
        {
            var sales = SalesWith(0, 0, 0, 0, 0,
                Rec("a", RecommendationTier.Normal, 5),
                Rec("b", RecommendationTier.Excellent, 5));

            var best = Sut().Compute(sales, 0).BestMatch;
            Assert.AreEqual("book_b", best.BookId);
            Assert.AreEqual(RecommendationTier.Excellent, best.Tier);
        }

        [Test]
        public void BestMatch_NoRecommendations_IsNull()
        {
            var best = Sut().Compute(SalesWith(0, 0, 0, 0, 0), 0).BestMatch;
            Assert.IsNull(best);
        }

        [Test]
        public void BestMatch_OnlySkipped_IsNull()
        {
            var sales = SalesWith(0, 0, 0, 0, 0,
                RecommendationResult.Skipped("r1"),
                RecommendationResult.Skipped("r2"));

            var best = Sut().Compute(sales, 0).BestMatch;
            Assert.IsNull(best);
        }
    }
}
