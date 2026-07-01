using System;
using Game.Conditions.API;

namespace Game.DayCycle.Conditions
{
    /// <summary>
    /// Leaf condition: "today's weather is <c>weatherId</c>". Compares the current day's weather
    /// (<see cref="ICurrentDayWeatherProvider"/>) to the configured value, case-insensitively. Both sides
    /// are normalized (an optional "weather_" prefix is stripped). ReasonKey is "weatherIs.{weatherId}".
    /// </summary>
    public sealed class WeatherIsCondition : ICondition
    {
        private readonly ICurrentDayWeatherProvider _provider;
        private readonly string _weatherId;

        public WeatherIsCondition(ICurrentDayWeatherProvider provider, string weatherId)
        {
            _provider = provider;
            _weatherId = Normalize(weatherId);
        }

        public ConditionResult Evaluate()
        {
            var current = Normalize(_provider.GetCurrentWeatherId());
            var met = !string.IsNullOrEmpty(_weatherId)
                      && string.Equals(current, _weatherId, StringComparison.OrdinalIgnoreCase);
            return ConditionResult.Boolean(met, $"weatherIs.{_weatherId}");
        }

        /// <summary>Strips a leading "weather_" (as found in ActiveModifierIds) to match DayConfig.WeatherId.</summary>
        internal static string Normalize(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            const string prefix = "weather_";
            return id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? id.Substring(prefix.Length) : id;
        }
    }
}
