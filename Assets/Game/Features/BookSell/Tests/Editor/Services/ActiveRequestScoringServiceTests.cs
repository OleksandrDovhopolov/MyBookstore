using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using Game.Configs.Models;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Services
{
    public sealed class ActiveRequestScoringServiceTests
    {
        private static RequestDefinitionConfig ConditionRequest(string quality) => new()
        {
            Id = "req_condition",
            Enabled = true,
            Conditions = new RequestConditionGroup
            {
                All = new[]
                {
                    new RequestCondition
                    {
                        Type = "qualities",
                        Operator = "contains",
                        Value = JToken.FromObject(quality)
                    }
                }
            }
        };

        private static ActiveRequestScoringService Sut()
            => new(new RecommendationScoringService(), new BookConditionRequestEvaluator());

        [Test]
        public void ConditionMatch_ReturnsExcellentAndFixedGold()
        {
            var request = ActiveRequestRuntime.FromCondition(ConditionRequest("Detective"), "debug");
            var book = SalesTestKit.Book("book", genre: "Crime", qualities: new[] { "Detective" });

            var result = Sut().Score(book, request, null);

            Assert.AreEqual(RecommendationTier.Excellent, result.Tier);
            Assert.AreEqual(BookConfig.FixedPriceGold, result.GoldEarned);
        }

        [Test]
        public void ConditionMismatch_ReturnsFailedAndZeroGold()
        {
            var request = ActiveRequestRuntime.FromCondition(ConditionRequest("Detective"), "debug");
            var book = SalesTestKit.Book("book", genre: "Crime", qualities: new[] { "Romance" });

            var result = Sut().Score(book, request, null);

            Assert.AreEqual(RecommendationTier.Failed, result.Tier);
            Assert.AreEqual(0, result.GoldEarned);
        }

        [Test]
        public void LegacyRuntime_DelegatesToLegacyScoring()
        {
            var request = SalesTestKit.ActiveRequest("legacy");
            var book = SalesTestKit.Book("book");

            var result = Sut().Score(book, request, SalesTestKit.Location());

            Assert.AreEqual(RecommendationTier.Excellent, result.Tier);
            Assert.AreEqual(BookConfig.FixedPriceGold + 25, result.GoldEarned);
        }
    }
}
