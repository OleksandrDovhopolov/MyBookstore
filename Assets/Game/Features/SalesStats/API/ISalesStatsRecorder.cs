namespace Game.SalesStats.API
{
    /// <summary>
    /// Write seam used by the Sales feature at the single sold-book chokepoint
    /// (<c>SalesDayCommitService</c>). The recorder resolves the book's genre itself, so callers only
    /// pass the book id (plus optional <see cref="SaleContext"/>). Recording is in-memory + batched: the
    /// persist happens later in the service's save hook, not per call (see <see cref="ISalesStatsReader"/>).
    /// </summary>
    public interface ISalesStatsRecorder
    {
        /// <summary>
        /// Counts one sold book toward its genre only (no location/day attribution). No-op for null/empty
        /// or unknown books. Convenience for callers without sale context (cheats, isolated tests).
        /// </summary>
        void RecordSold(string bookId);

        /// <summary>
        /// Counts one sold book toward its genre and, when present in <paramref name="ctx"/>, toward the
        /// per-location and per-day tallies. No-op for null/empty or unknown books.
        /// </summary>
        void RecordSold(string bookId, in SaleContext ctx);
    }
}
