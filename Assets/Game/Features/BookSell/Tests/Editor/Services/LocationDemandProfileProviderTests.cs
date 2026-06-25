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
        private static LocationDemandProfileProvider Provider(FakeConfigsService configs, int count)
            => new(configs, new SalesTuning { PassiveRequestGenreCount = count });

        [Test]
        public void SamplesDistinctGenresFromLocationDemand()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[]
            {
                SalesTestKit.Location("loc", demandGenres: new[] { "sci-fi", "mystery", "romance" })
            });
            var setup = new SalesSessionSetup(1, "loc", Array.Empty<string>());

            var profile = Provider(configs, 2).Create(setup, new FakeSalesRandom());

            Assert.AreEqual(2, profile.DesiredGenres.Count);
            Assert.AreEqual(2, profile.DesiredGenres.Distinct().Count(), "Genres are distinct.");
            CollectionAssert.IsSubsetOf(profile.DesiredGenres.ToArray(), new[] { "sci-fi", "mystery", "romance" });
        }

        [Test]
        public void ClampsRequestedCountToAvailableGenres()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { SalesTestKit.Location("loc", demandGenres: new[] { "sci-fi", "mystery" }) });
            var setup = new SalesSessionSetup(1, "loc", Array.Empty<string>());

            var profile = Provider(configs, 5).Create(setup, new FakeSalesRandom());

            Assert.AreEqual(2, profile.DesiredGenres.Count);
        }

        [Test]
        public void FallsBackToShelfGenres_WhenLocationHasNoDemand()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { SalesTestKit.Location("loc", demandGenres: Array.Empty<string>()) });
            configs.SetAll(new[]
            {
                SalesTestKit.Book("b1", genre: "history"),
                SalesTestKit.Book("b2", genre: "kids")
            });
            var setup = new SalesSessionSetup(1, "loc", new[] { "b1", "b2" });

            var profile = Provider(configs, 2).Create(setup, new FakeSalesRandom());

            Assert.AreEqual(2, profile.DesiredGenres.Count);
            CollectionAssert.IsSubsetOf(profile.DesiredGenres.ToArray(), new[] { "history", "kids" });
        }
    }
}
