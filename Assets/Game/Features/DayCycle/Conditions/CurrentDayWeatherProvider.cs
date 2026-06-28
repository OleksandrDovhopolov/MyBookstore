using System;
using Game.DayCycle.Day;
using Game.DayCycle.Morning;

namespace Game.DayCycle.Conditions
{
    /// <summary>
    /// Resolves the current day's weather by reusing <see cref="IMorningContextResolver"/> (which already
    /// maps a day index to a <c>DayConfig</c> with a deterministic fallback). Deliberately does NOT
    /// re-implement the DayConfig lookup, so weather conditions never diverge from the Morning/day context.
    /// </summary>
    public sealed class CurrentDayWeatherProvider : ICurrentDayWeatherProvider
    {
        private readonly IDayProgressService _dayProgress;
        private readonly IMorningContextResolver _resolver;

        public CurrentDayWeatherProvider(IDayProgressService dayProgress, IMorningContextResolver resolver)
        {
            _dayProgress = dayProgress ?? throw new ArgumentNullException(nameof(dayProgress));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        public string GetCurrentWeatherId()
            => _resolver.Resolve(_dayProgress.Current.CurrentDay).WeatherId ?? string.Empty;
    }
}
