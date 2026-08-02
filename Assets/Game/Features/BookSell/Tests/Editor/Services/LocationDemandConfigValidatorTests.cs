using System.Linq;
using Book.Sell.Domain;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using Game.Configs.Models;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Services
{
    public sealed class LocationDemandConfigValidatorTests
    {
        private static FakeConfigsService Configs(params LocationConfig[] locations)
        {
            var configs = new FakeConfigsService();
            configs.SetAll(locations);
            configs.SetAll(new[]
            {
                SalesTestKit.Book("b1", genre: "Classic"),
                SalesTestKit.Book("b2", genre: "Crime"),
                SalesTestKit.Book("b3", genre: "Drama"),
                SalesTestKit.Book("b4", genre: "Fact"),
                SalesTestKit.Book("b5", genre: "Fantasy"),
                SalesTestKit.Book("b6", genre: "Kids"),
                SalesTestKit.Book("b7", genre: "Travel")
            });
            return configs;
        }

        private static SalesTuning Tuning(int requestedCount = 2)
            => new() { PassiveRequestGenreCount = requestedCount };

        [Test]
        public void ValidCurrentStyleConfig_NoWarnings()
        {
            var configs = Configs(
                SalesTestKit.Location("loc_park", demandGenres: new[] { "Drama", "Kids", "Classic" }),
                SalesTestKit.Location("loc_port", demandGenres: new[] { "Travel", "Crime" }));

            var warnings = new LocationDemandConfigValidator(configs, Tuning()).Validate();

            Assert.AreEqual(0, warnings.Count);
        }

        [Test]
        public void Warns_WhenDemandGenresEmpty()
        {
            var configs = Configs(SalesTestKit.Location("loc", demandGenres: new string[0]));

            var warnings = new LocationDemandConfigValidator(configs, Tuning()).Validate();

            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains("no demandGenres", warnings[0]);
        }

        [Test]
        public void Warns_WhenDemandGenreIsDuplicated()
        {
            var configs = Configs(SalesTestKit.Location("loc", demandGenres: new[] { "Fantasy", "fantasy", "Fact" }));

            var warnings = new LocationDemandConfigValidator(configs, Tuning()).Validate();

            AssertHasWarning(warnings, "repeats demand genre");
        }

        [Test]
        public void Warns_WhenDemandGenreIsNotBookGenre()
        {
            var configs = Configs(SalesTestKit.Location("loc", demandGenres: new[] { "Fantasy", "Mystery" }));

            var warnings = new LocationDemandConfigValidator(configs, Tuning()).Validate();

            AssertHasWarning(warnings, "not a valid BookGenre");
        }

        [Test]
        public void Warns_WhenDemandGenreHasNoCatalogBooks()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { SalesTestKit.Location("loc", demandGenres: new[] { "Fantasy", "Travel" }) });
            configs.SetAll(new[]
            {
                SalesTestKit.Book("b1", genre: "Fantasy"),
                SalesTestKit.Book("b2", genre: "Crime"),
                SalesTestKit.Book("b3", genre: "Drama")
            });

            var warnings = new LocationDemandConfigValidator(configs, Tuning()).Validate();

            AssertHasWarning(warnings, "has no BookConfig.PrimaryGenre");
        }

        [Test]
        public void Warns_WhenDemandBucketIsSmallerThanRequestedProfile()
        {
            var configs = Configs(SalesTestKit.Location("loc", demandGenres: new[] { "Fantasy" }));

            var warnings = new LocationDemandConfigValidator(configs, Tuning(requestedCount: 2)).Validate();

            AssertHasWarning(warnings, "catalog-backed demand genres");
        }

        [Test]
        public void Warns_WhenOtherBucketIsSmallerThanRequestedProfile()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { SalesTestKit.Location("loc", demandGenres: new[] { "Fantasy", "Fact" }) });
            configs.SetAll(new[]
            {
                SalesTestKit.Book("b1", genre: "Fantasy"),
                SalesTestKit.Book("b2", genre: "Fact")
            });

            var warnings = new LocationDemandConfigValidator(configs, Tuning(requestedCount: 2)).Validate();

            AssertHasWarning(warnings, "non-demand catalog genres");
        }

        private static void AssertHasWarning(System.Collections.Generic.IEnumerable<string> warnings, string fragment)
        {
            Assert.IsTrue(
                warnings.Any(w => w.Contains(fragment)),
                $"Expected warning containing '{fragment}'.");
        }
    }
}
