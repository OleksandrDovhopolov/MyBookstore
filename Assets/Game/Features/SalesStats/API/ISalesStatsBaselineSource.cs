namespace Game.SalesStats.API
{
    /// <summary>
    /// Lets a caller (quests) freeze the current sold counters and read them relative to that snapshot
    /// ("since baseline"). Used for quest task progress that must count only sales after the task started
    /// (docs/QUESTS.md §11.2). Pure statistics — no dependency on the conditions engine.
    /// </summary>
    public interface ISalesStatsBaselineSource
    {
        /// <summary>Captures only counters requested by <paramref name="plan"/>.</summary>
        SalesStatsBaselineDto CaptureBaseline(SalesStatsBaselineCapturePlan plan);

        /// <summary>Reader returning <c>Max(0, live − baseline)</c> for every query.</summary>
        ISalesStatsReader CreateScopedReader(SalesStatsBaselineDto baseline);
    }
}
