using System;
using System.Collections.Generic;

namespace Game.DayCycle.Morning.Model
{
    /// <summary>
    /// Полезная нагрузка, которую утро отдаёт в Подготовку при нажатии «К подготовке».
    /// Содержит только то, что нужно следующим фазам для решения по локации/ассортименту.
    /// </summary>
    public sealed class MorningContinueResult
    {
        public int Day { get; set; }
        public string DayId { get; set; }
        public string EventId { get; set; }
        public string WeatherId { get; set; }

        public IReadOnlyList<string> ActiveModifierIds { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> DemandGenres { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> DemandTags { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> TargetLocationIds { get; set; } = Array.Empty<string>();
    }
}
