using System;
using System.Collections.Generic;
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
            return config != null ? FromConfig(dayIndex, config) : Fallback(dayIndex);
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

            // Нет точного совпадения — переиспользуем последний настроенный день,
            // чтобы дни за пределами контента не валились в пустой fallback.
            return match ?? lastByIndex;
        }

        private static MorningDayContext FromConfig(int dayIndex, DayConfig config)
        {
            return new MorningDayContext
            {
                Day = dayIndex,
                DayId = config.Id,
                Title = config.Title,
                WeatherId = config.WeatherId,
                EventId = config.EventId,
                SummaryText = config.SummaryText,
                HintText = config.HintText,
                DemandGenres = config.DemandGenres ?? Array.Empty<string>(),
                DemandTags = config.DemandTags ?? Array.Empty<string>(),
                TargetLocationIds = config.TargetLocationIds ?? Array.Empty<string>(),
                ActiveModifierIds = BuildModifierIds(config.WeatherId, config.EventId),
                IsFallback = false
            };
        }

        private static MorningDayContext Fallback(int dayIndex)
        {
            return new MorningDayContext
            {
                Day = dayIndex,
                DayId = $"fallback_day_{dayIndex}",
                Title = MorningFallback.Title,
                WeatherId = MorningFallback.WeatherId,
                EventId = MorningFallback.EventId,
                SummaryText = MorningFallback.SummaryText,
                HintText = MorningFallback.HintText,
                DemandGenres = Array.Empty<string>(),
                DemandTags = Array.Empty<string>(),
                TargetLocationIds = Array.Empty<string>(),
                ActiveModifierIds = Array.Empty<string>(),
                IsFallback = true
            };
        }

        /// <summary>
        /// Активные модификаторы дня = погода + событие (непустые), в формате,
        /// который ждут Подготовка/Продажа: "weather_&lt;id&gt;", "event_&lt;id&gt;".
        /// </summary>
        private static IReadOnlyList<string> BuildModifierIds(string weatherId, string eventId)
        {
            var modifiers = new List<string>(2);
            if (!string.IsNullOrWhiteSpace(weatherId))
                modifiers.Add($"weather_{weatherId}");
            if (!string.IsNullOrWhiteSpace(eventId))
                modifiers.Add($"event_{eventId}");
            return modifiers;
        }
    }
}
