using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using UnityEngine;

namespace Book.Sell.Services
{
    public sealed class ProfileMatchedRequestSelectorFactory : IActiveRequestSelectorFactory
    {
        private readonly IBookConditionRequestEvaluator _evaluator;

        public ProfileMatchedRequestSelectorFactory(IBookConditionRequestEvaluator evaluator)
        {
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        }

        public IActiveRequestSelector CreateForDay(IReadOnlyList<ActiveRequestRuntime> pool)
            => new ProfileMatchedRequestSelector(pool, _evaluator);
    }

    /// <summary>
    /// Draws one request per active customer, without repeating inside a day while the pool lasts.
    /// <para>
    /// Two filters, applied in order of how much they cost the player to lose:
    /// <b>answerable</b> — at least one book on the shelf right now satisfies the request — and
    /// <b>profile</b> — the request matches what this customer came in wanting. Answerability wins when the
    /// two disagree: a request in the wrong genre is a small oddity, a request nothing on the shelf can
    /// answer is a dead end the player reads as unfair.
    /// </para>
    /// </summary>
    public sealed class ProfileMatchedRequestSelector : IActiveRequestSelector
    {
        private const string LogTag = "[ActiveRequests]";

        private readonly IReadOnlyList<ActiveRequestRuntime> _pool;
        private readonly List<ActiveRequestRuntime> _remaining;
        private readonly IBookConditionRequestEvaluator _evaluator;

        public ProfileMatchedRequestSelector(
            IReadOnlyList<ActiveRequestRuntime> pool,
            IBookConditionRequestEvaluator evaluator)
        {
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));

            if (pool == null || pool.Count == 0)
            {
                _pool = Array.Empty<ActiveRequestRuntime>();
                _remaining = new List<ActiveRequestRuntime>();
                return;
            }

            var nonNullPool = new List<ActiveRequestRuntime>(pool.Count);
            for (var i = 0; i < pool.Count; i++)
                if (pool[i] != null)
                    nonNullPool.Add(pool[i]);

            _pool = nonNullPool.ToArray();
            _remaining = new List<ActiveRequestRuntime>(_pool);
        }

        public ActiveRequestRuntime Draw(CustomerProfile profile, IReadOnlyList<ShelfBook> shelf, ISalesRandom random)
        {
            if (_pool.Count == 0) return null;

            var onShelf = CountAvailable(shelf);
            var remainingBefore = _remaining.Count;
            var answerable = CollectAnswerable(_remaining, shelf);

            var matchIndex = PickMatchingIndex(_remaining, answerable, profile, random);
            if (matchIndex >= 0)
                return DrawRemainingAt(matchIndex);

            if (answerable.Count > 0)
            {
                // Nothing the customer asked for can be answered today, but something else can. Better an
                // off-profile request the player can win than an on-profile one they cannot.
                var request = DrawRemainingAt(answerable[PickIndex(answerable.Count, random)]);
                Debug.Log($"{LogTag} no answerable request matches profile [{FormatGenres(profile)}]; " +
                          $"fell back to '{request.Id}' (shelf={onShelf}, " +
                          $"answerable={answerable.Count}/{remainingBefore}).");
                return request;
            }

            if (remainingBefore > 0)
            {
                var request = DrawRemainingAt(PickIndex(remainingBefore, random));
                Debug.LogWarning($"{LogTag} no remaining request can be answered from the current shelf " +
                                 $"(shelf={onShelf}, remaining={remainingBefore}); " +
                                 $"issuing '{request.Id}' anyway — the player can only skip it.");
                return request;
            }

            Debug.LogWarning($"{LogTag} all active requests were already used; repeating from full pool " +
                             $"for profile [{FormatGenres(profile)}].");
            return _pool[PickIndex(_pool.Count, random)];
        }

        /// <summary>Indices into <paramref name="candidates"/> that at least one shelf book can satisfy.</summary>
        private List<int> CollectAnswerable(
            IReadOnlyList<ActiveRequestRuntime> candidates,
            IReadOnlyList<ShelfBook> shelf)
        {
            var answerable = new List<int>();
            if (shelf == null || shelf.Count == 0) return answerable;

            for (var i = 0; i < candidates.Count; i++)
            {
                if (IsAnswerable(candidates[i], shelf))
                    answerable.Add(i);
            }

            return answerable;
        }

        private bool IsAnswerable(ActiveRequestRuntime request, IReadOnlyList<ShelfBook> shelf)
        {
            var definition = request?.ConditionRequest;
            if (definition == null) return false;

            for (var i = 0; i < shelf.Count; i++)
            {
                var book = shelf[i];
                if (book?.Config == null || book.State != ShelfBookState.Available) continue;
                if (_evaluator.Evaluate(book.Config, definition).IsMatch) return true;
            }

            return false;
        }

        private static int PickMatchingIndex(
            IReadOnlyList<ActiveRequestRuntime> candidates,
            List<int> answerable,
            CustomerProfile profile,
            ISalesRandom random)
        {
            var matching = new List<int>();
            for (var i = 0; i < answerable.Count; i++)
            {
                var index = answerable[i];
                if (candidates[index]?.MatchesProfile(profile?.DesiredGenres) == true)
                    matching.Add(index);
            }

            if (matching.Count == 0) return -1;
            return matching[PickIndex(matching.Count, random)];
        }

        private ActiveRequestRuntime DrawRemainingAt(int index)
        {
            var request = _remaining[index];
            _remaining.RemoveAt(index);
            return request;
        }

        private static int PickIndex(int count, ISalesRandom random)
        {
            if (count <= 1) return 0;
            return random?.Range(0, count) ?? 0;
        }

        private static int CountAvailable(IReadOnlyList<ShelfBook> shelf)
        {
            if (shelf == null) return 0;

            var count = 0;
            for (var i = 0; i < shelf.Count; i++)
                if (shelf[i] != null && shelf[i].State == ShelfBookState.Available) count++;
            return count;
        }

        private static string FormatGenres(CustomerProfile profile)
        {
            var genres = profile?.DesiredGenres;
            return genres == null || genres.Count == 0 ? "<none>" : string.Join(",", genres);
        }
    }
}
