using System;

namespace Game.SalesStats.API
{
    /// <summary>
    /// Full contract: read-only counters (<see cref="ISalesStatsReader"/>), the write seam
    /// (<see cref="ISalesStatsRecorder"/>), and a <see cref="Changed"/> signal so reactive consumers
    /// (e.g. the location-unlock recompute) know when a counter moved. <see cref="Changed"/> is an
    /// in-memory event — it is NOT a persistence trigger and NOT the source of truth.
    /// </summary>
    public interface ISalesStatsService : ISalesStatsReader, ISalesStatsRecorder
    {
        event Action<SalesStatsChange> Changed;
    }
}
