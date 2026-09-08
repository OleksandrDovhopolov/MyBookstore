using System;
using Game.Configs;
using Game.Configs.Models;
using Game.DayCycle.Morning.Model;

namespace Game.DayCycle.Morning
{
    /// <inheritdoc cref="IMorningContextResolver"/>
    public sealed class MorningContextResolver : IMorningContextResolver
    {
        private readonly IConfigsService _configs;

        public MorningContextResolver(IConfigsService configs)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        public MorningDayContext Resolve(int dayIndex)
        {
            var config = FindByDayIndex(dayIndex);
            var context = config != null ? FromConfig(dayIndex, config) : Fallback(dayIndex);
            return context;
        }

        private DayConfig FindByDayIndex(int dayIndex)
        {
            DayConfig match = null;
            DayConfig lastByIndex = null;

            foreach (var day in _configs.GetAll<DayConfig>())
            {
                if (day.DayIndex == dayIndex)
                {
                    match = day;
                    break;
                }

                if (lastByIndex == null || day.DayIndex > lastByIndex.DayIndex)
                    lastByIndex = day;
            }

            return match ?? lastByIndex;
        }

        private static MorningDayContext FromConfig(int dayIndex, DayConfig config)
        {
            return new MorningDayContext
            {
                Day = dayIndex,
                DayId = config.Id,
                Title = config.TitleKey
            };
        }

        private static MorningDayContext Fallback(int dayIndex)
        {
            return new MorningDayContext
            {
                Day = dayIndex,
                DayId = $"fallback_day_{dayIndex}"
            };
        }
    }
}
