using Game.Conditions.API;
using Game.Conditions.Services;
using Game.DayCycle.Conditions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.DayCycle.Tests.Editor
{
    /// <summary>
    /// "weatherIs" condition parsed by the engine → evaluated against a fake current-weather seam.
    /// </summary>
    public sealed class WeatherIsConditionTests
    {
        private static IConditionParser Parser(FakeCurrentDayWeatherProvider weather)
            => new ConditionParser(new ConditionFactoryRegistry(new IConditionFactory[]
            {
                new WeatherIsConditionFactory(weather)
            }));

        private static JObject Node(string weatherId)
            => new JObject { ["type"] = WeatherIsConditionFactory.TypeId, ["weatherId"] = weatherId };

        [Test]
        public void Met_WhenWeatherMatches_CaseInsensitive()
        {
            var weather = new FakeCurrentDayWeatherProvider { WeatherId = "Snow" };
            var condition = Parser(weather).Parse(Node("snow"));

            var result = condition.Evaluate();
            Assert.IsTrue(result.IsMet);
            Assert.AreEqual("weatherIs.snow", result.ReasonKey);
        }

        [Test]
        public void NotMet_WhenWeatherDiffers()
        {
            var weather = new FakeCurrentDayWeatherProvider { WeatherId = "clear" };
            Assert.IsFalse(Parser(weather).Parse(Node("snow")).Evaluate().IsMet);
        }

        [Test]
        public void NormalizesWeatherPrefix_OnBothSides()
        {
            // Config passes "weather_snow", provider returns bare "snow" — still matches.
            var weather = new FakeCurrentDayWeatherProvider { WeatherId = "snow" };
            Assert.IsTrue(Parser(weather).Parse(Node("weather_snow")).Evaluate().IsMet);

            // And vice versa: provider returns prefixed, config bare.
            var weather2 = new FakeCurrentDayWeatherProvider { WeatherId = "weather_snow" };
            Assert.IsTrue(Parser(weather2).Parse(Node("snow")).Evaluate().IsMet);
        }

        [Test]
        public void EmptyWeatherId_FailsClosed()
        {
            var weather = new FakeCurrentDayWeatherProvider { WeatherId = "snow" };
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex("factory 'weatherIs' failed"));

            Assert.IsFalse(Parser(weather).Parse(Node("")).Evaluate().IsMet);
        }

        private sealed class FakeCurrentDayWeatherProvider : ICurrentDayWeatherProvider
        {
            public string WeatherId = string.Empty;
            public string GetCurrentWeatherId() => WeatherId;
        }
    }
}
