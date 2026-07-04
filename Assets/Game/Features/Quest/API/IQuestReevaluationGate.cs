using System;

namespace Game.Quest.API
{
    /// <summary>
    /// Lets a batch operation suspend quest re-evaluation for its duration, so quests react once to the final
    /// state instead of to each intermediate change. Used by the sales-day commit so a quest's sales baseline
    /// is captured after the day's sales are recorded, not mid-commit.
    /// </summary>
    public interface IQuestReevaluationGate
    {
        /// <summary>
        /// Suspends re-evaluation until the returned handle is disposed. On the last dispose a single
        /// re-evaluation runs if anything requested one while suspended. Nesting-safe.
        /// </summary>
        IDisposable SuspendReevaluation();
    }
}
