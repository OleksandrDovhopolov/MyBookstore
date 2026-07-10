using System.Threading;
using Game.Conditions.API;
using Game.Conditions.Services;
using Game.Configs.Models;
using Game.SalesStats.API;
using Game.SalesStats.Conditions;
using Game.SalesStats.Services;
using Game.SalesStats.Tests.Editor.Fakes;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.SalesStats.Tests.Editor
{
    /// <summary>
    /// End-to-end of the location/day conditions: sales recorded into <see cref="SalesStatsService"/> →
    /// <c>soldGenreAtLocation</c> / <c>soldGenreInSingleDay</c> parsed by the engine → evaluated against
    /// the read-only seam.
    /// </summary>
    public sealed class SalesStatsConditionsTests
    {
        private const string FantasyBook = "book_fantasy";
        private const string CrimeBook = "book_crime";
        private const string FarBeach = "far_beach";
        private const string CafeLiberte = "cafe_liberte";

        private static (SalesStatsService svc, IConditionParser parser) Build()
        {
            var configs = new FakeConfigsService()
                .Add(new BookConfig { Id = FantasyBook, Genre = BookGenre.Fantasy.ToConfigValue() })
                .Add(new BookConfig { Id = CrimeBook, Genre = BookGenre.Crime.ToConfigValue() });

            var svc = new SalesStatsService(new FakeSaveService(), new FakeSalesStatsRepository(), configs);
            svc.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();

            var registry = new ConditionFactoryRegistry(new IConditionFactory[]
            {
                new SoldGenreAtLocationConditionFactory(svc),
                new SoldGenreInSingleDayConditionFactory(svc)
            });
            return (svc, new ConditionParser(registry));
        }

        private static JObject AtLocation(BookGenre genre, string locationId, int min)
            => new JObject
            {
                ["type"] = SoldGenreAtLocationConditionFactory.TypeId,
                ["genre"] = genre.ToConfigValue(),
                ["locationId"] = locationId,
                ["min"] = min
            };

        private static JObject InSingleDay(BookGenre genre, int min)
            => new JObject
            {
                ["type"] = SoldGenreInSingleDayConditionFactory.TypeId,
                ["genre"] = genre.ToConfigValue(),
                ["min"] = min
            };

        private static void Sell(SalesStatsService svc, string bookId, string locationId, int day, int times)
        {
            for (var i = 0; i < times; i++) svc.RecordSold(bookId, new SaleContext(locationId, day));
        }

        [Test]
        public void SoldGenreAtLocation_OnlyCountsThatLocation()
        {
            var (svc, parser) = Build();
            var condition = parser.Parse(AtLocation(BookGenre.Fantasy, FarBeach, 3));

            // Sales elsewhere do not count toward the Far Beach goal.
            Sell(svc, FantasyBook, CafeLiberte, 1, 5);
            Assert.IsFalse(condition.Evaluate().IsMet);

            Sell(svc, FantasyBook, FarBeach, 1, 2);
            Assert.IsFalse(condition.Evaluate().IsMet);

            Sell(svc, FantasyBook, FarBeach, 1, 1);
            var result = condition.Evaluate();
            Assert.IsTrue(result.IsMet);
            Assert.AreEqual(3, result.Current);
            Assert.AreEqual(3, result.Target);
            Assert.AreEqual($"soldGenreAtLocation.{FarBeach}.{BookGenre.Fantasy}", result.ReasonKey);
        }

        [Test]
        public void SoldGenreInSingleDay_MetByBestDay_NotLifetime()
        {
            var (svc, parser) = Build();
            var condition = parser.Parse(InSingleDay(BookGenre.Fantasy, 15));

            // 10 across day 1, 10 across day 2 => lifetime 20 but best single day is 10: not met.
            Sell(svc, FantasyBook, FarBeach, 1, 10);
            Sell(svc, FantasyBook, FarBeach, 2, 10);
            Assert.IsFalse(condition.Evaluate().IsMet, "Lifetime >= 15 but no single day reaches 15.");

            // 15 in a single day => met.
            Sell(svc, FantasyBook, FarBeach, 3, 15);
            var result = condition.Evaluate();
            Assert.IsTrue(result.IsMet);
            Assert.AreEqual(15, result.Current);
            Assert.AreEqual(15, result.Target);
        }

        [Test]
        public void Composite_All_LocationAndSingleDay()
        {
            var (svc, parser) = Build();
            var node = new JObject
            {
                ["all"] = new JArray
                {
                    AtLocation(BookGenre.Fantasy, FarBeach, 5),
                    InSingleDay(BookGenre.Fantasy, 5)
                }
            };
            var condition = parser.Parse(node);

            Sell(svc, FantasyBook, FarBeach, 7, 5);
            Assert.IsTrue(condition.Evaluate().IsMet, "5 Fantasy at Far Beach in one day satisfies both leaves.");
        }
    }
}
