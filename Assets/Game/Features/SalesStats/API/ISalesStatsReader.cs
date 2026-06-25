using Game.Configs.Models;

namespace Game.SalesStats.API
{
    /// <summary>
    /// Read-only seam over persistent per-genre sold-book counters. This is the surface that unlock
    /// conditions (e.g. <c>SoldGenreAtLeast</c>) depend on — they never touch the writable service or
    /// the event bus. Same sync-read model as <c>IProgressionService.Reputation</c>.
    /// </summary>
    public interface ISalesStatsReader
    {
        /// <summary>Total books sold of <paramref name="genre"/> across all days. Always &gt;= 0.</summary>
        int GetSold(BookGenre genre);

        /// <summary>Total books sold across every genre. Always &gt;= 0.</summary>
        int TotalSold { get; }
    }
}
