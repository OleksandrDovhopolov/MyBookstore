using System;
using System.Collections.Generic;

namespace Game.DayCycle.Morning.Model
{
    /// <summary>
    /// Разрешённый контекст утра: то, что показывается на экране и передаётся дальше по дню.
    /// Резолвится детерминированно из DayConfig по номеру дня (или fallback), поэтому
    /// перезапуск на фазе утра показывает тот же день/событие/погоду без отдельного сохранения.
    /// </summary>
    public sealed class MorningDayContext
    {
        public int Day { get; set; }
        public string DayId { get; set; }
        public string Title { get; set; }
        public string WeatherId { get; set; }
        public string EventId { get; set; }
        public string SummaryText { get; set; }
        public string HintText { get; set; }

        public IReadOnlyList<string> DemandGenres { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> DemandTags { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> TargetLocationIds { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Активные модификаторы спроса дня (например, "weather_clear", "event_exam_week").
        /// Производятся резолвером из погоды/события и читаются Подготовкой/Продажей.
        /// </summary>
        public IReadOnlyList<string> ActiveModifierIds { get; set; } = Array.Empty<string>();

        /// <summary>true, если контекст собран из fallback (нет подходящего DayConfig).</summary>
        public bool IsFallback { get; set; }
    }
}
