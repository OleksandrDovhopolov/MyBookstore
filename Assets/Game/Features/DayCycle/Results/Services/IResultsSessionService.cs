using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.DayCycle.Results.Domain;

namespace Game.DayCycle.Results.Services
{
    /// <summary>
    /// Orchestrates the Results phase: loads the persisted SalesDayResult, applies rewards
    /// idempotently, exposes the summary for the View, and advances to the next day.
    /// </summary>
    public interface IResultsSessionService
    {
        /// <summary>Latest summary built by the service. Null until <see cref="LoadAsync"/> succeeds.</summary>
        ResultsSummary CurrentSummary { get; }

        /// <summary>Fires when a summary is ready (either freshly applied or restored from save).</summary>
        event Action<ResultsSummary> SummaryReady;

        /// <summary>Fires when no SalesDayResult is available in save — the View shows an error state.</summary>
        event Action NoResultAvailable;

        /// <summary>
        /// Load SalesDayResult from save, apply rewards (idempotent), emit a summary.
        /// Safe to call repeatedly: after the first apply, subsequent calls just rebuild the summary
        /// without mutating balances.
        /// </summary>
        UniTask LoadAndApplyAsync(CancellationToken ct);

        /// <summary>
        /// Advance the global day-state to (completedDay + 1, Morning) and trigger the gameplay
        /// scene reload. The View disables the button afterwards.
        /// </summary>
        UniTask AdvanceToNextDayAsync(CancellationToken ct);
    }
}
