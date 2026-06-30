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

        // Provider is acquired LAZILY (Func), not in the ctor. Building the IConditionFactory collection
        // happens while IConditionParser is being constructed; eagerly pulling ICurrentDayWeatherProvider here
        // would chain CurrentDayWeatherProvider → IMorningContextResolver → ILocationUnlockService →
        // IConditionParser and form a DI cycle (VContainer Lazy self-reference). The provider is resolved on the
        // first Create() — at runtime, after the graph is built — so the chain is safe (parser already exists).
        private readonly Func<ICurrentDayWeatherProvider> _providerFactory;
        private ICurrentDayWeatherProvider _provider;

        public WeatherIsConditionFactory(Func<ICurrentDayWeatherProvider> providerFactory)
            => _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));

        public string Type => TypeId;

        public ICondition Create(JObject node)
        {
            var weatherId = node.Value<string>("weatherId");
            if (string.IsNullOrEmpty(weatherId))
                throw new ArgumentException("missing 'weatherId'");

            _provider ??= _providerFactory() ?? throw new InvalidOperationException(
                "ICurrentDayWeatherProvider resolved to null for the weatherIs factory.");
            return new WeatherIsCondition(_provider, weatherId);
        }
    }
}
