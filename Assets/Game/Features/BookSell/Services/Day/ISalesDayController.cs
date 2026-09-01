using System;
using System.Threading;
using Book.Sell.API;
using Book.Sell.Domain;
using Cysharp.Threading.Tasks;
using Dialogue;

namespace Book.Sell.Services
{
    //TODO check logic of all event. is this a good solution ? 
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
        ActiveRequestRuntime CurrentRequest { get; }

        /// <summary>Current lifecycle phase of the day (Running / ReadyToClose / Completed).</summary>
        SalesDayPhase Phase { get; }

        bool IsDayCompleted { get; }

        /// <summary>Fired once when the day becomes concludable (no more customers, all spawned ones Done).
        /// The view shows the "close shop" CTA in response; the day does NOT auto-complete.</summary>
        event Action DayReadyToClose;

        event Action<ActiveRequestRuntime> ActiveRequestStarted;

        /// <summary>A customer acquired the interaction lock and a scripted dialogue opened for them. The
        /// customer is carried so world-HUD presentation knows whom to anchor the dialogue to; presentation
        /// subscribes and drives completion via <see cref="CompleteDialogue"/>. Subscribe BEFORE the first
        /// <see cref="Tick"/> (same contract as the other events) — the event is not replayed for late
        /// subscribers.</summary>
        event Action<int, string> DayStarted;
        event Action<Customer, DialoguePayload> DialogueStarted;
        event Action<RecommendationResult> RecommendationResolved;
        event Action<PassiveSaleEvent> PassiveSaleHappened;
        event Action<Customer, RecommendationResult> CustomerRecommendationResolved;
        event Action<Customer, PassiveSaleEvent> CustomerPassiveSaleHappened;
        event Action<Customer, CustomerCommentPayload> CustomerCommented;
        event Action<Customer, string> CustomerPassivePurchaseFailed;

        /// <summary>The customer finished its visit; the <c>int</c> is <c>purchasedBookCount</c>
        /// (active recommendations + passive sales, >= 1). For the HUD completion bubble/animation.</summary>
        event Action<Customer, int> CustomerPurchaseCompleted;

        /// <summary>The customer started leaving — the View should clear its thought bubble.</summary>
        event Action<Customer> CustomerThoughtBubbleHidden;
        event Action<SalesDayResult> DayCompleted;

        /// <summary>Fired whenever any customer changes phase (arrival / browsing / leaving / done). For the View.</summary>
        event Action<Customer> CustomerPhaseChanged;

        /// <summary>Customer targeted a book and reserved it (soft-lock) before committing the sale. For the View's feedback log.</summary>
        event Action<Customer, string> BookReserved;

        /// <summary>A reservation was released without a sale (customer aborted mid-purchase). For the View's feedback log.</summary>
        event Action<Customer, string> BookReleased;

        /// <summary>Fired when the shelf's sellable inventory changes or is rebuilt for a new day.</summary>
        event Action ShelfChanged;

        UniTask StartDayAsync(int day, CancellationToken ct);

        /// <summary>Advance the simulation by dt seconds. No-op while the interaction lock is held (domain pause) or the day is done.</summary>
        void Tick(float dt);

        /// <summary>Player picked a book for the current active minigame.</summary>
        void RecommendBook(string bookId);

        /// <summary>Player declined to recommend anything for the current active minigame.</summary>
        void SkipCurrentRequest();

        /// <summary>Presentation closed the scripted dialogue UI: completes the current <c>DialogStep</c>
        /// (releasing the interaction lock) and advances the customer's plan. No-op if no dialogue is open.
        /// Mirror of <see cref="SkipCurrentRequest"/> for the dialogue path.</summary>
        void CompleteDialogue();

        /// <summary>Player closed the shop. Valid only while <see cref="Phase"/> is
        /// <see cref="SalesDayPhase.ReadyToClose"/>; publishes the result and fires
        /// <see cref="DayCompleted"/>. No-op in any other phase.</summary>
        void ConcludeDay();

        /// <summary>
        /// Forcibly ends the current sales day. Debug/cheat use only.
        /// When <paramref name="zeroOut"/> is true the published result has no sales, no gold and
        /// no served customers (only <see cref="Day"/> is preserved). Otherwise the already
        /// accumulated result is published as-is.
        /// Safe to call mid-minigame or mid-dialogue: active request / dialogue state is dropped, the lock
        /// is left held but no longer reachable (Tick short-circuits on the completed flag). No-op if the
        /// day has already completed.
        /// </summary>
        void ForceCompleteDay(bool zeroOut);
    }
}
