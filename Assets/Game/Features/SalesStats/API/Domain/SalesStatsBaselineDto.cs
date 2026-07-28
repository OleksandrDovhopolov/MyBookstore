using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.SalesStats.API
{
    /// <summary>
    /// Quest-local sales baseline captured when a sales task becomes active. New captures are compact and
    /// keep only counters referenced by that task.
    /// </summary>
    public sealed class SalesStatsBaselineDto
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, int> SoldByGenre { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, Dictionary<string, int>> SoldByLocationGenre { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, SalesStatsSingleDayBaselineDto> SoldInSingleDayGenre { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, int> ExcellentPicksByGenre { get; set; }
    }

    public sealed class SalesStatsSingleDayBaselineDto
    {
        public int ActivationDay { get; set; }

        public int ActivationDayCount { get; set; }
    }
}
