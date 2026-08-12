using System;
using System.Linq;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using Game.Configs.Models;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Services
{
    public sealed class ActiveRequestGenreCoverageValidatorTests
    {
        [Test]
        public void Validate_NoErrors_WhenEveryBookGenreHasEnabledRequest()
        {
            var configs = ConfigsWithRequests(
                Request("classic", "Classic"),
                Request("crime", "Crime"),
                Request("drama", "Drama"),
                Request("fact", "Fact"),
                Request("fantasy", "Fantasy"),
                Request("kids", "Kids"),
                Request("travel", "Travel"));

            var report = new ActiveRequestGenreCoverageValidator(configs).Validate();

            Assert.AreEqual(0, report.Errors.Count);
        }

        [Test]
        public void Validate_Errors_WhenBookGenreHasNoEnabledRequest()
        {
            var configs = ConfigsWithRequests(Request("classic", "Classic"));

            var report = new ActiveRequestGenreCoverageValidator(configs).Validate();

            Assert.IsTrue(report.Errors.Any(x => x.Contains("genre 'Crime' has no enabled active request")));
        }

        [Test]
        public void Validate_Warns_WhenRequestHasNoResolvedGenres()
        {
            var configs = ConfigsWithRequests(
                Request("classic", "Classic"),
                Request("crime", "Crime"),
                Request("drama", "Drama"),
                Request("fact", "Fact"),
                Request("fantasy", "Fantasy"),
                Request("kids", "Kids"),
                Request("travel", "Travel"),
                QualityOnlyRequest("quality_only"));

            var report = new ActiveRequestGenreCoverageValidator(configs).Validate();

            AssertHasWarning(report, "request 'quality_only' has no resolved genres");
        }

        [Test]
        public void Validate_Warns_WhenMetadataGenreDiffersFromResolvedGenres()
        {
            var configs = ConfigsWithRequests(
                Request("req_scarlet_02", "Classic", metadataGenre: "Crime"),
                Request("crime", "Crime"),
                Request("drama", "Drama"),
                Request("fact", "Fact"),
                Request("fantasy", "Fantasy"),
                Request("kids", "Kids"),
                Request("travel", "Travel"));

            var report = new ActiveRequestGenreCoverageValidator(configs).Validate();

            AssertHasWarning(report, "request 'req_scarlet_02' metadata genre 'Crime' is not included");
        }

        private static FakeConfigsService ConfigsWithRequests(params RequestDefinitionConfig[] requests)
        {
            var configs = new FakeConfigsService();
            configs.SetAll(requests);
            return configs;
        }

        private static RequestDefinitionConfig Request(
            string id,
            string conditionGenre,
            string metadataGenre = null,
            bool enabled = true)
            => new()
            {
                Id = id,
                Genre = metadataGenre ?? conditionGenre,
                Enabled = enabled,
                Conditions = new RequestConditionGroup
                {
                    All = new[] { Condition("genres", "contains", conditionGenre) }
                }
            };

        private static RequestDefinitionConfig QualityOnlyRequest(string id)
            => new()
            {
                Id = id,
                Enabled = true,
                Conditions = new RequestConditionGroup
                {
                    All = new[] { Condition("qualities", "contains", "detective") }
                }
            };

        private static RequestCondition Condition(string type, string op, object value)
            => new()
            {
                Type = type,
                Operator = op,
                Value = JToken.FromObject(value)
            };

        private static void AssertHasWarning(ActiveRequestGenreCoverageReport report, string fragment)
        {
            Assert.IsTrue(
                report.Warnings.Any(w => w.Contains(fragment)),
                $"Expected warning containing '{fragment}'.");
        }
    }
}
