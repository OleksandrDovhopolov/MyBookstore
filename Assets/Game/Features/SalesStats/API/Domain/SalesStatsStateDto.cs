using System.Collections.Generic;

namespace Game.SalesStats.API
{
    /// <summary>
    /// Transport DTO between <see cref="ISalesStatsService"/> and <see cref="ISalesStatsRepository"/>.
    /// Holds sold counts keyed by genre config value (<c>BookGenre.ToConfigValue()</c>). Newer aggregates
    /// (per-location, per-day) live in this same DTO; older saves simply omit them (schema migration is a
    /// no-op fill with empty maps).
    /// </summary>
    public sealed class SalesStatsStateDto
    {
        /// <summary>Lifetime sold count per genre config value.</summary>
        public Dictionary<string, int> SoldByGenre { get; set; }

        /// <summary>Lifetime sold count per location id, then per genre config value. Feeds the location info window.</summary>
        public Dictionary<string, Dictionary<string, int>> SoldByLocationGenre { get; set; }

        /// <summary>Sold count per game day (1-based), then per genre config value. Feeds the monthly calendar
        /// (sum across genres) and the "sold N of a genre in a single day" quest condition. Unbounded in MVP.</summary>
        public Dictionary<int, Dictionary<string, int>> SoldByDayGenre { get; set; }
    }
}
