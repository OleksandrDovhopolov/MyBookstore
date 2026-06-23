using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.DayCycle.Results.Domain;

namespace Game.DayCycle.Results.Services
{
    public interface IResultsSessionService
    {
        ResultsSummary CurrentSummary { get; }

        event Action<ResultsSummary> SummaryReady;

        event Action NoResultAvailable;

        UniTask LoadAndApplyAsync(CancellationToken ct);

        UniTask AdvanceToNextDayAsync(CancellationToken ct);
    }
}
