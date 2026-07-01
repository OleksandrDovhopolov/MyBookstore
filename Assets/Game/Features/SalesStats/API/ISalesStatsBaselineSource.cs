namespace Game.SalesStats.API
{
    /// <summary>
    /// Lets a caller (quests) freeze the current sold counters and read them relative to that snapshot
    /// ("since baseline"). Used for quest task progress that must count only sales after the task started
    /// (docs/QUESTS.md §11.2). Pure statistics — no dependency on the conditions engine.
    /// </summary>
    public interface ISalesStatsBaselineSource
    {
        /// <summary>Deep copy of the current counters; pass it back to <see cref="CreateScopedReader"/>.</summary>
        SalesStatsStateDto CaptureBaseline();

        /// <summary>Reader returning <c>Max(0, live − baseline)</c> for every query.</summary>
        ISalesStatsReader CreateScopedReader(SalesStatsStateDto baseline);
    }
}
