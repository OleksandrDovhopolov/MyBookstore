namespace Game.SalesStats.API
{
    /// <summary>
    /// Write seam used by the Sales feature at the single sold-book chokepoint
    /// (<c>SoldBookCommitter</c>). The recorder resolves the book's genre itself, so callers only
    /// pass the book id. Recording is in-memory + batched: the persist happens later in the service's
    /// save hook, not per call (see <see cref="ISalesStatsReader"/> notes).
    /// </summary>
    public interface ISalesStatsRecorder
    {
        /// <summary>Counts one sold book toward its genre. No-op for null/empty or unknown books.</summary>
        void RecordSold(string bookId);
    }
}
