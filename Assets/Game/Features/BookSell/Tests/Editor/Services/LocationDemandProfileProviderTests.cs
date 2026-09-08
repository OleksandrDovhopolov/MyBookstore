using System;
using System.Linq;
using Book.Sell.Domain;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using Game.Configs.Models;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Services
{
    public sealed class LocationDemandProfileProviderTests
    {
        private static LocationDemandProfileProvider Provider(
            FakeConfigsService configs,
            int count,
            double wideShare = 0.70d,
            double narrowShare = 0.50d,
            int threshold = 3)
        {
            var tuning = new SalesTuning
            {
                PassiveRequestGenreCount = count,
                PassiveDemandRequestShare = wideShare,
                PassiveDemandRequestShareNarrow = narrowShare,
                NarrowDemandGenreThreshold = threshold
            };
            return new(configs, tuning);
        }

        private static FakeConfigsService Configs(LocationConfig location, params BookConfig[] books)
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { location });
            configs.SetAll(books);
            return configs;
        }

        [Test]
        public void SamplesGenresFromCatalog_NotShelf()
        {
            var configs = Configs(
                SalesTestKit.Location("loc", demandGenres: new[] { "Fantasy" }),
                SalesTestKit.Book("b1", genre: "Fantasy"),
                SalesTestKit.Book("b2", genre: "Crime"));
            var setup = new SalesSessionSetup(1, "loc", new[] { "b2" });

            var profile = Provider(configs, 1)
                .Create(setup, new FakeSalesRandom().EnqueueDouble(0.0d).EnqueueRangeIndex(0));

            Assert.AreEqual(1, profile.DesiredGenres.Count);
            Assert.AreEqual("Fantasy", profile.DesiredGenres[0]);
        }

        [Test]
        public void EmptyShelf_DoesNotReturnEmptyProfile_WhenCatalogHasGenres()
        {
            var configs = Configs(
                SalesTestKit.Location("loc", demandGenres: new[] { "Fantasy", "Fact", "Classic" }),
                SalesTestKit.Book("b1", genre: "Fantasy"),
                SalesTestKit.Book("b2", genre: "Fact"),
                SalesTestKit.Book("b3", genre: "Classic"));
            var setup = new SalesSessionSetup(1, "loc", Array.Empty<string>());

            var profile = Provider(configs, 2)
                .Create(setup, new FakeSalesRandom()
                    .EnqueueDouble(0.0d, 0.0d)
                    .EnqueueRangeIndex(0, 0));

            Assert.AreEqual(2, profile.DesiredGenres.Count);
        }

        [Test]
        public void DemandAndOtherSlotsUseShareAndRange()
        {
            var configs = Configs(
                SalesTestKit.Location("loc", demandGenres: new[] { "Fantasy", "Fact", "Classic" }),
                SalesTestKit.Book("b1", genre: "Fantasy"),
                SalesTestKit.Book("b2", genre: "Fact"),
                SalesTestKit.Book("b3", genre: "Classic"),
                SalesTestKit.Book("b4", genre: "Crime"));
            var setup = new SalesSessionSetup(1, "loc", Array.Empty<string>());

            var profile = Provider(configs, 2)
                .Create(setup, new FakeSalesRandom()
                    .EnqueueDouble(0.69d, 0.70d)
                    .EnqueueRangeIndex(1, 0));

            CollectionAssert.AreEqual(new[] { "Fact", "Crime" }, profile.DesiredGenres);
        }

        [Test]
        public void SamplesDistinctGenres()
        {
            var configs = Configs(
                SalesTestKit.Location("loc", demandGenres: new[] { "Fantasy", "Fact", "Classic" }),
                SalesTestKit.Book("b1", genre: "Fantasy"),
                SalesTestKit.Book("b2", genre: "Fact"),
                SalesTestKit.Book("b3", genre: "Classic"),
                SalesTestKit.Book("b4", genre: "Crime"));
            var setup = new SalesSessionSetup(1, "loc", Array.Empty<string>());

            var profile = Provider(configs, 4)
                .Create(setup, new FakeSalesRandom()
                    .EnqueueDouble(0.0d, 0.0d, 0.0d, 0.0d)
                    .EnqueueRangeIndex(0, 0, 0, 0));

            Assert.AreEqual(4, profile.DesiredGenres.Count);
            Assert.AreEqual(4, profile.DesiredGenres.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        [Test]
        public void TwoDemandGenresUseNarrowShare_ThreeUseWideShare()
        {
            var catalog = new[]
            {
                SalesTestKit.Book("b1", genre: "Fantasy"),
                SalesTestKit.Book("b2", genre: "Fact"),
                SalesTestKit.Book("b3", genre: "Classic"),
                SalesTestKit.Book("b4", genre: "Crime")
            };
            var setup = new SalesSessionSetup(1, "loc", Array.Empty<string>());

            var narrow = Provider(
                    Configs(SalesTestKit.Location("loc", demandGenres: new[] { "Fantasy", "Fact" }), catalog),
                    1)
                .Create(setup, new FakeSalesRandom().EnqueueDouble(0.55d).EnqueueRangeIndex(0));
            var wide = Provider(
                    Configs(SalesTestKit.Location("loc", demandGenres: new[] { "Fantasy", "Fact", "Classic" }), catalog),
                    1)
                .Create(setup, new FakeSalesRandom().EnqueueDouble(0.55d).EnqueueRangeIndex(0));

            Assert.AreEqual("Classic", narrow.DesiredGenres[0]);
            Assert.AreEqual("Fantasy", wide.DesiredGenres[0]);
        }

        [Test]
        public void EmptyDemandBucket_FallsBackToOtherGenres()
        {
            var configs = Configs(
                SalesTestKit.Location("loc", demandGenres: new[] { "Mystery" }),
                SalesTestKit.Book("b1", genre: "Fantasy"),
                SalesTestKit.Book("b2", genre: "Crime"));
            var setup = new SalesSessionSetup(1, "loc", Array.Empty<string>());

            var profile = Provider(configs, 1)
                .Create(setup, new FakeSalesRandom().EnqueueDouble(0.0d).EnqueueRangeIndex(1));

            Assert.AreEqual("Crime", profile.DesiredGenres[0]);
        }

        [Test]
        public void ClampsRequestedCountToCatalogGenres()
        {
            var configs = Configs(
                SalesTestKit.Location("loc", demandGenres: new[] { "Fantasy", "Fact", "Classic" }),
                SalesTestKit.Book("b1", genre: "Fantasy"),
                SalesTestKit.Book("b2", genre: "Crime"));
            var setup = new SalesSessionSetup(1, "loc", Array.Empty<string>());

            var profile = Provider(configs, 5)
                .Create(setup, new FakeSalesRandom()
                    .EnqueueDouble(0.0d, 0.99d)
                    .EnqueueRangeIndex(0, 0));

            Assert.AreEqual(2, profile.DesiredGenres.Count);
        }

        [Test]
        public void EmptyCatalog_ReturnsEmptyProfile()
        {
            var configs = Configs(SalesTestKit.Location("loc", demandGenres: new[] { "Fantasy" }));
            var setup = new SalesSessionSetup(1, "loc", Array.Empty<string>());

            var profile = Provider(configs, 2).Create(setup, new FakeSalesRandom());

            Assert.AreEqual(0, profile.DesiredGenres.Count);
        }

        [Test, Category("Balance")]
        public void DemandSlotShare_MatchesConfiguredWideAndNarrowShares()
        {
            AssertDemandShare(
                SalesTestKit.Location("loc", demandGenres: new[] { "Fantasy", "Fact", "Classic" }),
                expected: 0.70d);
            AssertDemandShare(
                SalesTestKit.Location("loc", demandGenres: new[] { "Fantasy", "Fact" }),
                expected: 0.50d);
        }

        private static void AssertDemandShare(LocationConfig location, double expected)
        {
            var configs = Configs(
                location,
                SalesTestKit.Book("b1", genre: "Fantasy"),
                SalesTestKit.Book("b2", genre: "Fact"),
                SalesTestKit.Book("b3", genre: "Classic"),
                SalesTestKit.Book("b4", genre: "Crime"),
                SalesTestKit.Book("b5", genre: "Drama"),
                SalesTestKit.Book("b6", genre: "Kids"),
                SalesTestKit.Book("b7", genre: "Travel"));
            var setup = new SalesSessionSetup(1, location.Id, Array.Empty<string>());
            var provider = Provider(configs, 2);
            var demand = new System.Collections.Generic.HashSet<string>(
                location.DemandGenres,
                StringComparer.OrdinalIgnoreCase);

            const int profiles = 50;
            const int expectedSlotsPerProfile = 2;
            var expectedTotalSlots = profiles * expectedSlotsPerProfile;
            var expectedDemandSlots = (int)Math.Round(expected * expectedTotalSlots);
            var demandRoll = Math.Max(0d, expected - 0.001d);
            var rolls = Enumerable.Range(0, expectedTotalSlots)
                .Select(i => i < expectedDemandSlots ? demandRoll : expected)
                .ToArray();
            var ranges = Enumerable.Repeat(0, expectedTotalSlots).ToArray();

            var demandSlots = 0;
            var totalSlots = 0;
            var random = new FakeSalesRandom()
                .EnqueueDouble(rolls)
                .EnqueueRangeIndex(ranges);
            for (var i = 0; i < profiles; i++)
            {
                var profile = provider.Create(setup, random);
                totalSlots += profile.DesiredGenres.Count;
                for (var j = 0; j < profile.DesiredGenres.Count; j++)
                    if (demand.Contains(profile.DesiredGenres[j]))
                        demandSlots++;
            }

            var actual = (double)demandSlots / totalSlots;
            TestContext.WriteLine($"location,{location.Id},expected,{expected:0.00},actual,{actual:0.0000},demandSlots,{demandSlots},totalSlots,{totalSlots}");
            Assert.That(actual, Is.EqualTo(expected).Within(0.01d));
        }
    }
}
