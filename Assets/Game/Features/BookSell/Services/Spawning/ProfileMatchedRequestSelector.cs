using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using UnityEngine;

namespace Book.Sell.Services
{
    public sealed class ProfileMatchedRequestSelectorFactory : IActiveRequestSelectorFactory
    {
        public IActiveRequestSelector CreateForDay(IReadOnlyList<ActiveRequestRuntime> pool)
            => new ProfileMatchedRequestSelector(pool);
    }

    public sealed class ProfileMatchedRequestSelector : IActiveRequestSelector
    {
        private const string LogTag = "[ActiveRequests]";

        private readonly IReadOnlyList<ActiveRequestRuntime> _pool;
        private readonly List<ActiveRequestRuntime> _remaining;

        public ProfileMatchedRequestSelector(IReadOnlyList<ActiveRequestRuntime> pool)
        {
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

        public ActiveRequestRuntime Draw(CustomerProfile profile, ISalesRandom random)
        {
            if (_pool.Count == 0) return null;

            var matchIndex = PickMatchingIndex(_remaining, profile, random);
            if (matchIndex >= 0)
                return DrawRemainingAt(matchIndex);

            if (_remaining.Count > 0)
            {
                Debug.LogWarning($"{LogTag} no request matches customer profile [{FormatGenres(profile)}]; using any remaining request.");
                return DrawRemainingAt(PickIndex(_remaining.Count, random));
            }

            Debug.LogWarning($"{LogTag} all active requests were already used; repeating from full pool for profile [{FormatGenres(profile)}].");
            return _pool[PickIndex(_pool.Count, random)];
        }

        private static int PickMatchingIndex(
            IReadOnlyList<ActiveRequestRuntime> candidates,
            CustomerProfile profile,
            ISalesRandom random)
        {
            var matching = new List<int>();
            for (var i = 0; i < candidates.Count; i++)
            {
                if (candidates[i]?.MatchesProfile(profile?.DesiredGenres) == true)
                    matching.Add(i);
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

        private static string FormatGenres(CustomerProfile profile)
        {
            var genres = profile?.DesiredGenres;
            return genres == null || genres.Count == 0 ? "<none>" : string.Join(",", genres);
        }
    }
}
