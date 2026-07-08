using Game.Configs.Models;
using Game.DayCycle.Morning;
using Game.DayCycle.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Game.DayCycle.Tests.Editor
{
    public sealed class MorningContextResolverTests
    {
        private static DayConfig Day1() => new()
        {
            Id = "day_001",
            DayIndex = 1,
            Title = "Первый день у парка",
        };

        private static MorningContextResolver ResolverWith(params DayConfig[] days)
        {
            var configs = new FakeConfigsService();
            configs.SetAll(days);
            return new MorningContextResolver(configs);
        }

        [Test]
        public void Resolve_MatchingDay_MapsAllFields()
        {
            var ctx = ResolverWith(Day1()).Resolve(1);

            Assert.AreEqual(1, ctx.Day);
            Assert.AreEqual("day_001", ctx.DayId);
            Assert.AreEqual("Первый день у парка", ctx.Title);
        }

        [Test]
        public void Resolve_NoConfigsAtAll_UsesDeterministicFallback()
        {
            var ctx = ResolverWith().Resolve(1);

            Assert.AreEqual(1, ctx.Day);
            Assert.AreEqual(MorningFallback.Title, ctx.Title);
        }

        [Test]
        public void Resolve_DayBeyondContent_ReusesLastConfiguredDay()
        {
            var day2 = Day1();
            day2.Id = "day_002";
            day2.DayIndex = 2;
            day2.Title = "Второй день";

            var ctx = ResolverWith(Day1(), day2).Resolve(99);

            Assert.AreEqual("day_002", ctx.DayId);
            Assert.AreEqual(99, ctx.Day, "Day отражает запрошенный номер, контент берётся от последнего настроенного дня.");
        }

        [Test]
        public void Resolve_IsDeterministic_SameDaySameResult()
        {
            var resolver = ResolverWith(Day1());
            var first = resolver.Resolve(1);
            var second = resolver.Resolve(1);

            Assert.AreEqual(first.DayId, second.DayId);
            Assert.AreEqual(first.Title, second.Title);
        }
    }
}
