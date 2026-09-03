namespace Game.SalesStats.API
{
    /// <summary>
    /// Write seam used by the Sales feature at the single sold-book chokepoint
    /// (<c>SalesDayCommitService</c>). The recorder uses <see cref="SaleContext.SoldGenre"/> when the
    /// sale source can attribute one, otherwise it falls back to the book's primary genre. Recording is
    /// in-memory + batched: the persist happens later in the service's save hook, not per call.
    /// </summary>
    public interface ISalesStatsRecorder
    {
        /// <summary>
        /// Counts one sold book toward its genre only (no location/day attribution). No-op for null/empty
        /// or unknown books. Debug/test convenience for callers without sale context.
        /// </summary>
        void RecordSold(string bookId);

        /// <summary>
        /// Counts one sold book toward the attributed/fallback genre and, when present in <paramref name="ctx"/>, toward the
        /// per-location and per-day tallies. No-op for null/empty or unknown books.
        /// </summary>
        void RecordSold(string bookId, in SaleContext ctx);

        /// <summary>
        /// Counts one successful active recommendation toward the attributed/fallback genre. This does
        /// not count as a sale and does not affect <see cref="ISalesStatsReader.TotalSold"/>.
        /// </summary>
        void RecordActivePick(string bookId, in SaleContext ctx);
    }
}
