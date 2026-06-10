using System.Linq;
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
            WeatherId = "clear",
            EventId = "exam_week",
            SummaryText = "summary",
            HintText = "hint",
            DemandGenres = new[] { "science", "classic" },
            DemandTags = new[] { "short" },
            TargetLocationIds = new[] { "loc_downtown" }
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

            Assert.IsFalse(ctx.IsFallback);
            Assert.AreEqual(1, ctx.Day);
            Assert.AreEqual("day_001", ctx.DayId);
            Assert.AreEqual("Первый день у парка", ctx.Title);
            Assert.AreEqual("clear", ctx.WeatherId);
            Assert.AreEqual("exam_week", ctx.EventId);
            CollectionAssert.AreEqual(new[] { "science", "classic" }, ctx.DemandGenres);
            CollectionAssert.AreEqual(new[] { "short" }, ctx.DemandTags);
            CollectionAssert.AreEqual(new[] { "loc_downtown" }, ctx.TargetLocationIds);
        }

        [Test]
        public void Resolve_MatchingDay_BuildsWeatherAndEventModifiers()
        {
            var ctx = ResolverWith(Day1()).Resolve(1);
            CollectionAssert.AreEqual(new[] { "weather_clear", "event_exam_week" }, ctx.ActiveModifierIds);
        }

        [Test]
        public void Resolve_EmptyEvent_OmitsEventModifier()
        {
            var day = Day1();
            day.EventId = "";
            var ctx = ResolverWith(day).Resolve(1);
            CollectionAssert.AreEqual(new[] { "weather_clear" }, ctx.ActiveModifierIds);
        }

        [Test]
        public void Resolve_NoConfigsAtAll_UsesDeterministicFallback()
        {
            var ctx = ResolverWith().Resolve(1);

            Assert.IsTrue(ctx.IsFallback);
            Assert.AreEqual(1, ctx.Day);
            Assert.AreEqual(MorningFallback.Title, ctx.Title);
            Assert.IsEmpty(ctx.ActiveModifierIds);
            Assert.IsEmpty(ctx.DemandGenres);
        }

        [Test]
        public void Resolve_DayBeyondContent_ReusesLastConfiguredDay()
        {
            var day2 = Day1();
            day2.Id = "day_002";
            day2.DayIndex = 2;
            day2.Title = "Второй день";

            var ctx = ResolverWith(Day1(), day2).Resolve(99);

            Assert.IsFalse(ctx.IsFallback);
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
            CollectionAssert.AreEqual(first.ActiveModifierIds.ToArray(), second.ActiveModifierIds.ToArray());
        }
    }
}
