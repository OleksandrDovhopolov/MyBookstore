using System;
using System.Threading;
using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Services;
using Cysharp.Threading.Tasks;
using Dialogue;
using UnityEngine;

namespace Game.Cheat
{
    /// <summary>
    /// Minimal standalone <see cref="ISalesDayController"/> for the active-sale cheat: drives
    /// <c>RecommendationMinigameWindow</c> with a fixed request + shelf and no real sales day, customer,
    /// interaction lock, or persistence. Only the members the window touches do anything
    /// (<see cref="CurrentRequest"/>, <see cref="Shelf"/>, <see cref="RecommendationResolved"/>,
    /// <see cref="RecommendBook"/>, <see cref="SkipCurrentRequest"/>); everything else is inert.
    /// </summary>
    public sealed class CheatActiveRequestController : ISalesDayController
    {
        private const string LogPrefix = "[ActiveSaleCheat]";

        private readonly ActiveRequestRuntime _request;
        private readonly SalesShelf _shelf;
        private readonly IActiveRequestScoringService _scoring;
        private readonly SalesDayResult _result = new();

        public CheatActiveRequestController(
            ActiveRequestRuntime request, SalesShelf shelf, IActiveRequestScoringService scoring)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _shelf = shelf ?? throw new ArgumentNullException(nameof(shelf));
            _scoring = scoring ?? throw new ArgumentNullException(nameof(scoring));
        }

        public int Day => 0;
        public string LocationId => null;
        public SalesShelf Shelf => _shelf;
        public SalesDayResult AccumulatedResult => _result;
        public ActiveRequestRuntime CurrentRequest => _request;
        public SalesDayPhase Phase => SalesDayPhase.Running;
        public bool IsDayCompleted => false;

        // Only RecommendationResolved is real — the window subscribes to it to show the result panel.
        public event Action<RecommendationResult> RecommendationResolved;

        // Inert events (no backing delegate) so the interface is satisfied without CS0067 warnings.
        public event Action DayReadyToClose { add { } remove { } }
        public event Action<ActiveRequestRuntime> ActiveRequestStarted { add { } remove { } }
        public event Action<Customer, DialoguePayload> DialogueStarted { add { } remove { } }
        public event Action<PassiveSaleEvent> PassiveSaleHappened { add { } remove { } }
        public event Action<Customer, RecommendationResult> CustomerRecommendationResolved { add { } remove { } }
        public event Action<Customer, PassiveSaleEvent> CustomerPassiveSaleHappened { add { } remove { } }
        public event Action<Customer, CustomerCommentPayload> CustomerCommented { add { } remove { } }
        public event Action<Customer, string> CustomerPassivePurchaseFailed { add { } remove { } }
        public event Action<Customer, int> CustomerPurchaseCompleted { add { } remove { } }
        public event Action<Customer> CustomerThoughtBubbleHidden { add { } remove { } }
        public event Action<SalesDayResult> DayCompleted { add { } remove { } }
        public event Action<Customer> CustomerPhaseChanged { add { } remove { } }
        public event Action<Customer, string> BookReserved { add { } remove { } }
        public event Action<Customer, string> BookReleased { add { } remove { } }
        public event Action ShelfChanged { add { } remove { } }

        public void RecommendBook(string bookId)
        {
            var shelfBook = _shelf.Find(bookId);
            if (shelfBook == null || shelfBook.State != ShelfBookState.Available || _shelf.IsReserved(bookId))
            {
                Debug.LogWarning($"{LogPrefix} book '{bookId}' is not available — ignored.");
                return;
            }

            var result = _scoring.Score(shelfBook.Config, _request, location: null);
            if (result.Tier == RecommendationTier.Excellent)
                _shelf.CommitSale(bookId);   // cosmetic: grey out the picked card if the window stays open

            Debug.Log($"{LogPrefix} request='{_request.Id}' book='{bookId}' tier={result.Tier} gold={result.GoldEarned}");
            RecommendationResolved?.Invoke(result);
        }

        public void SkipCurrentRequest()
            => RecommendationResolved?.Invoke(RecommendationResult.Skipped(_request.Id));

        // Inert lifecycle — nothing to drive standalone.
        public UniTask StartDayAsync(int day, CancellationToken ct) => UniTask.CompletedTask;
        public void Tick(float dt) { }
        public void CompleteDialogue() { }
        public void ConcludeDay() { }
        public void ForceCompleteDay(bool zeroOut) { }
    }
}
