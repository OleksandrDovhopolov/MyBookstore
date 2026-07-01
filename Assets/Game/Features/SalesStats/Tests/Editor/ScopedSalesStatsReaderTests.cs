using System.Threading;
using Game.Configs.Models;
using Game.SalesStats.API;
using Game.SalesStats.Services;
using Game.SalesStats.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Game.SalesStats.Tests.Editor
{
    /// <summary>
    /// Baseline scoped reader (4b): reads live counters minus a frozen snapshot ("sold since baseline").
    /// </summary>
    public sealed class ScopedSalesStatsReaderTests
    {
        private const string FantasyBook = "book_fantasy";
        private const string FarBeach = "far_beach";
        private const string Cafe = "cafe";

        private static SalesStatsService Build()
        {
            var configs = new FakeConfigsService()
                .Add(new BookConfig { Id = FantasyBook, Genre = BookGenre.Fantasy.ToConfigValue() });
            var svc = new SalesStatsService(new FakeSaveService(), new FakeSalesStatsRepository(), configs);
            svc.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            return svc;
        }

        private static void Sell(SalesStatsService svc, string location, int day, int times)
        {
            for (var i = 0; i < times; i++) svc.RecordSold(FantasyBook, new SaleContext(location, day));
        }

        [Test]
        public void ScopedReader_SubtractsBaseline_GenreAndLocation()
        {
            var svc = Build();
            Sell(svc, FarBeach, 1, 3);                 // pre-baseline

            var baseline = ((ISalesStatsBaselineSource)svc).CaptureBaseline();
            Sell(svc, FarBeach, 1, 2);                 // post-baseline (+2)

            var scoped = ((ISalesStatsBaselineSource)svc).CreateScopedReader(baseline);
            Assert.AreEqual(2, scoped.GetSold(BookGenre.Fantasy));
            Assert.AreEqual(2, scoped.GetSold(BookGenre.Fantasy, FarBeach));
            Assert.AreEqual(0, scoped.GetSold(BookGenre.Fantasy, Cafe));
            // Lifetime reader unaffected.
            Assert.AreEqual(5, svc.GetSold(BookGenre.Fantasy));
        }

        [Test]
        public void ScopedReader_SingleDay_CountsBestDaySinceBaseline()
        {
            var svc = Build();
            Sell(svc, FarBeach, 1, 3);                 // day 1 pre-baseline = 3

            var baseline = ((ISalesStatsBaselineSource)svc).CaptureBaseline();
            Sell(svc, FarBeach, 1, 1);                 // day 1 → +1 (scoped day1 = 1)
            Sell(svc, FarBeach, 2, 4);                 // day 2 → +4 (scoped day2 = 4)

            var scoped = ((ISalesStatsBaselineSource)svc).CreateScopedReader(baseline);
            // scoped: day1 = 4-3 = 1, day2 = 4-0 = 4 → best = 4.
            Assert.AreEqual(4, scoped.GetMaxSoldInSingleDay(BookGenre.Fantasy));
            // lifetime: day1 = 4, day2 = 4 → best = 4.
            Assert.AreEqual(4, svc.GetMaxSoldInSingleDay(BookGenre.Fantasy));
        }

        [Test]
        public void CaptureBaseline_IsIndependentSnapshot()
        {
            var svc = Build();
            Sell(svc, FarBeach, 1, 3);

            var baseline = ((ISalesStatsBaselineSource)svc).CaptureBaseline();
            Sell(svc, FarBeach, 1, 5);                 // mutate after capture

            Assert.AreEqual(3, baseline.SoldByGenre[BookGenre.Fantasy.ToConfigValue()]); // snapshot frozen
        }
    }
}
