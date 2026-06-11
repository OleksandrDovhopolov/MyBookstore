using System;
using System.Threading;
using Book.Sell.Domain;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    /// <summary>
    /// Day orchestrator for the Sales phase. The View subscribes to four events and keeps
    /// a local snapshot of the state.
    ///
    /// Event flow per interaction tick:
    ///   RecommendBook(id) / SkipCurrentRequest()
    ///     -> RecommendationResolved(result)
    ///     -> PassiveSaleHappened * 0..2
    ///     -> if queue empty -> DayCompleted(result)
    ///          else         -> ActiveRequestStarted(nextRequest)
    /// </summary>
    public interface ISalesSessionService
    {
        SalesSessionState State { get; }
        SalesDayResult AccumulatedResult { get; }

        /// <summary>The current active request, or null before StartDay / after the day is completed.</summary>
        RequestConfig CurrentRequest { get; }

        event Action<RequestConfig> ActiveRequestStarted;
        event Action<RecommendationResult> RecommendationResolved;
        event Action<PassiveSaleEvent> PassiveSaleHappened;
        event Action<SalesDayResult> DayCompleted;

        /// <summary>Builds the setup, generates the active queue, emits the first ActiveRequestStarted.</summary>
        UniTask StartDayAsync(int day, CancellationToken ct);

        /// <summary>The player picked a book from the shelf and pressed "Confirm".</summary>
        void RecommendBook(string bookId);

        /// <summary>The player pressed "Nothing to offer" — Skipped tier, no penalty.</summary>
        void SkipCurrentRequest();
    }
}
