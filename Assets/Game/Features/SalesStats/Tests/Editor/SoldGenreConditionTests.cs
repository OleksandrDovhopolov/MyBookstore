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
    /// End-to-end of the vertical slice: sales recorded into <see cref="SalesStatsService"/> →
    /// <c>soldGenre</c> condition parsed by the engine → evaluated against the read-only seam.
    /// </summary>
    public sealed class SoldGenreConditionTests
    {
        private const string CrimeBook = "book_crime";
        private const string KidsBook = "book_kids";

        private static (SalesStatsService svc, IConditionParser parser) Build()
        {
            var configs = new FakeConfigsService()
                .Add(new BookConfig { Id = CrimeBook, Genre = BookGenre.Crime.ToConfigValue() })
                .Add(new BookConfig { Id = KidsBook, Genre = BookGenre.Kids.ToConfigValue() });

            var svc = new SalesStatsService(new FakeSaveService(), new FakeSalesStatsRepository(), configs);
            svc.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();

            // The factory ships with SalesStats and reads the same service through ISalesStatsReader.
            var registry = new ConditionFactoryRegistry(new IConditionFactory[]
            {
                new SoldGenreConditionFactory(svc)
            });
            return (svc, new ConditionParser(registry));
        }

        private static JObject SoldGenre(BookGenre genre, int min)
            => new JObject { ["type"] = SoldGenreConditionFactory.TypeId, ["genre"] = genre.ToConfigValue(), ["min"] = min };

        private static void Sell(SalesStatsService svc, string bookId, int times)
        {
            for (var i = 0; i < times; i++) svc.RecordSold(bookId);
        }

        [Test]
        public void SoldGenre_MetWhenThresholdReached()
        {
            var (svc, parser) = Build();
            var condition = parser.Parse(SoldGenre(BookGenre.Crime, 3));

            Assert.IsFalse(condition.Evaluate().IsMet);

            Sell(svc, CrimeBook, 3);

            var result = condition.Evaluate();
            Assert.IsTrue(result.IsMet);
            Assert.AreEqual(3, result.Current);
            Assert.AreEqual(3, result.Target);
            Assert.AreEqual($"soldGenre.{BookGenre.Crime}", result.ReasonKey);
        }

        [Test]
        public void CombinedRequirement_30Crime_And_5Kids()
        {
            var (svc, parser) = Build();
            var node = new JObject
            {
                ["all"] = new JArray { SoldGenre(BookGenre.Crime, 30), SoldGenre(BookGenre.Kids, 5) }
            };
            var condition = parser.Parse(node);

            Sell(svc, CrimeBook, 30);
            Sell(svc, KidsBook, 4);
            Assert.IsFalse(condition.Evaluate().IsMet, "Kids requirement not yet met.");

            Sell(svc, KidsBook, 1);
            Assert.IsTrue(condition.Evaluate().IsMet, "Both genre thresholds reached.");
        }

        [Test]
        public void UnknownGenre_FailsClosed()
        {
            var (_, parser) = Build();
            // Factory throws on unknown genre; parser turns it into a never-met condition.
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex("factory 'soldGenre' failed"));

            var node = new JObject { ["type"] = SoldGenreConditionFactory.TypeId, ["genre"] = "NotAGenre", ["min"] = 1 };
            Assert.IsFalse(parser.Parse(node).Evaluate().IsMet);
        }
    }
}
