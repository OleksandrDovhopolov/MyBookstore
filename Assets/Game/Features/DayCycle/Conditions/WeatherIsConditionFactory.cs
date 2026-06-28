using System;
using Game.Conditions.API;
using Newtonsoft.Json.Linq;

namespace Game.DayCycle.Conditions
{
    /// <summary>
    /// Builds <see cref="WeatherIsCondition"/> from <c>{ "type": "weatherIs", "weatherId": "snow" }</c>.
    /// Canonical value is the bare DayConfig weather id (e.g. "snow"); a "weather_" prefix is tolerated
    /// and normalized away by the condition. Registered in DI by the DayCycle binding.
    /// </summary>
    public sealed class WeatherIsConditionFactory : IConditionFactory
    {
        public const string TypeId = "weatherIs";

        private readonly ICurrentDayWeatherProvider _provider;

        public WeatherIsConditionFactory(ICurrentDayWeatherProvider provider)
            => _provider = provider ?? throw new ArgumentNullException(nameof(provider));

        public string Type => TypeId;

        public ICondition Create(JObject node)
        {
            var weatherId = node.Value<string>("weatherId");
            if (string.IsNullOrEmpty(weatherId))
                throw new ArgumentException("missing 'weatherId'");

            return new WeatherIsCondition(_provider, weatherId);
        }
    }
}
