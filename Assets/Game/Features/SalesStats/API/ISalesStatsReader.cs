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
        /// <summary>Total books sold of <paramref name="genre"/> across all days and locations. Always &gt;= 0.</summary>
        int GetSold(BookGenre genre);

        /// <summary>Total books sold across every genre. Always &gt;= 0.</summary>
        int TotalSold { get; }

        /// <summary>Lifetime books sold of <paramref name="genre"/> at <paramref name="locationId"/>. Always &gt;= 0.</summary>
        int GetSold(BookGenre genre, string locationId);

        /// <summary>Total books sold on game day <paramref name="day"/> across every genre (for the calendar). Always &gt;= 0.</summary>
        int GetSoldOnDay(int day);

        /// <summary>Books of <paramref name="genre"/> sold on game day <paramref name="day"/>. Always &gt;= 0.</summary>
        int GetSoldOnDay(int day, BookGenre genre);

        /// <summary>Largest single-day count of <paramref name="genre"/> across all recorded days
        /// (backs the "sold N of a genre in a single day" condition). Always &gt;= 0.</summary>
        int GetMaxSoldInSingleDay(BookGenre genre);
    }
}
