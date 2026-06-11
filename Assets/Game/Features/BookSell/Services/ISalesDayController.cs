using System;
using System.Threading;
using Book.Sell.Domain;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    /// <summary>
    /// Drives the real-time sales day: spawns customers over time, ticks their plans, arbitrates the
    /// single active-minigame lock, and resolves player input. Replaces the turn-based
    /// SalesSessionService. The View subscribes to the events and pumps <see cref="Tick"/> from Update.
    /// </summary>
    public interface ISalesDayController
    {
        int Day { get; }
        string LocationId { get; }
        SalesShelf Shelf { get; }
        SalesDayResult AccumulatedResult { get; }

        /// <summary>Request of the customer currently in the active minigame (holding the lock), or null.</summary>
        RequestConfig CurrentRequest { get; }

        bool IsDayCompleted { get; }

        event Action<RequestConfig> ActiveRequestStarted;
        event Action<RecommendationResult> RecommendationResolved;
        event Action<PassiveSaleEvent> PassiveSaleHappened;
        event Action<SalesDayResult> DayCompleted;

        /// <summary>Fired whenever any customer changes phase (arrival / browsing / leaving / done). For the View.</summary>
        event Action<Customer> CustomerPhaseChanged;

        UniTask StartDayAsync(int day, CancellationToken ct);

        /// <summary>Advance the simulation by dt seconds. No-op while the interaction lock is held (domain pause) or the day is done.</summary>
        void Tick(float dt);

        /// <summary>Player picked a book for the current active minigame.</summary>
        void RecommendBook(string bookId);

        /// <summary>Player declined to recommend anything for the current active minigame.</summary>
        void SkipCurrentRequest();
    }
}
