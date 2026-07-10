using System;
using Game.DayCycle.Day;
using Game.DayCycle.Morning;

namespace Game.DayCycle.Conditions
{
    public sealed class NoOpWeatherProvider : ICurrentDayWeatherProvider
    {
        private readonly IDayProgressService _dayProgress;
        private readonly IMorningContextResolver _resolver;

        public NoOpWeatherProvider(IDayProgressService dayProgress, IMorningContextResolver resolver)
        {
            _dayProgress = dayProgress ?? throw new ArgumentNullException(nameof(dayProgress));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        public string GetCurrentWeatherId() => string.Empty;
    }
}
