using System;
using System.Linq;
using Book.Sell.Domain;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Services
{
    public sealed class LocationDemandProfileProviderTests
    {
        private static LocationDemandProfileProvider Provider(
            FakeConfigsService configs,
            int count,
            double demandWeight = 1.10d)
        {
            var tuning = new SalesTuning
            {
                PassiveRequestGenreCount = count,
                PassiveDemandGenreWeight = demandWeight
            };
            return new(configs, tuning, new SalesTuningDemandGenreWeightProvider(tuning));
        }

        [Test]
        public void SamplesDistinctGenresFromShelf_NotLocationDemandWhitelist()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[]
            {
                SalesTestKit.Location("loc", demandGenres: new[] { "sci-fi" })
            });
            configs.SetAll(new[]
            {
                SalesTestKit.Book("b1", genre: "sci-fi"),
                SalesTestKit.Book("b2", genre: "history"),
                SalesTestKit.Book("b3", genre: "kids")
            });
            var setup = new SalesSessionSetup(1, "loc", new[] { "b1", "b2", "b3" });

            var profile = Provider(configs, 3).Create(setup, new FakeSalesRandom());

            Assert.AreEqual(3, profile.DesiredGenres.Count);
            Assert.AreEqual(3, profile.DesiredGenres.Distinct().Count(), "Genres are distinct.");
            CollectionAssert.AreEquivalent(new[] { "sci-fi", "history", "kids" }, profile.DesiredGenres);
        }

        [Test]
        public void ClampsRequestedCountToAvailableGenres()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { SalesTestKit.Location("loc", demandGenres: new[] { "sci-fi", "mystery", "romance" }) });
            configs.SetAll(new[]
            {
                SalesTestKit.Book("b1", genre: "history"),
                SalesTestKit.Book("b2", genre: "kids")
            });
            var setup = new SalesSessionSetup(1, "loc", new[] { "b1", "b2" });

            var profile = Provider(configs, 5).Create(setup, new FakeSalesRandom());

            Assert.AreEqual(2, profile.DesiredGenres.Count);
        }

        [Test]
        public void DemandGenresUseConfiguredWeight_WhenSampling()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { SalesTestKit.Location("loc", demandGenres: new[] { "sci-fi" }) });
            configs.SetAll(new[]
            {
                SalesTestKit.Book("b1", genre: "history"),
                SalesTestKit.Book("b2", genre: "sci-fi")
            });
            var setup = new SalesSessionSetup(1, "loc", new[] { "b1", "b2" });

            var profile = Provider(configs, 1, demandWeight: 1.10d)
                .Create(setup, new FakeSalesRandom().EnqueueDouble(0.49d));

            Assert.AreEqual(1, profile.DesiredGenres.Count);
            Assert.AreEqual("sci-fi", profile.DesiredGenres[0]);
        }

        [Test]
        public void NonDemandShelfGenreCanBeSampled()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { SalesTestKit.Location("loc", demandGenres: new[] { "sci-fi" }) });
            configs.SetAll(new[]
            {
                SalesTestKit.Book("b1", genre: "history"),
                SalesTestKit.Book("b2", genre: "sci-fi")
            });
            var setup = new SalesSessionSetup(1, "loc", new[] { "b1", "b2" });

            var profile = Provider(configs, 1, demandWeight: 1.10d)
                .Create(setup, new FakeSalesRandom().EnqueueDouble(0.1d));

            Assert.AreEqual(1, profile.DesiredGenres.Count);
            Assert.AreEqual("history", profile.DesiredGenres[0]);
        }

        [Test]
        public void EmptyShelf_ReturnsEmptyProfile()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { SalesTestKit.Location("loc", demandGenres: new[] { "sci-fi" }) });
            var setup = new SalesSessionSetup(1, "loc", Array.Empty<string>());

            var profile = Provider(configs, 2).Create(setup, new FakeSalesRandom());

            Assert.AreEqual(0, profile.DesiredGenres.Count);
        }
    }
}
