using System.Collections.Generic;

namespace Game.SalesStats.API
{
    /// <summary>
    /// Transport DTO between <see cref="ISalesStatsService"/> and <see cref="ISalesStatsRepository"/>.
    /// Holds sold counts keyed by genre config value (<c>BookGenre.ToConfigValue()</c>). Future sales
    /// aggregates (per-tag, per-location, ...) live in this same DTO.
    /// </summary>
    public sealed class SalesStatsStateDto
    {
        public Dictionary<string, int> SoldByGenre { get; set; }
    }
}
