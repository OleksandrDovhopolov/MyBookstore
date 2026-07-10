using System;
using System.Collections.Generic;
using Book.Sell.Domain;

namespace Book.Sell.Services
{
    /// <summary>
    /// Legacy passive model behind the resolver seam: delegates to <see cref="IPassiveSaleSelector"/>
    /// (roll every shelf genre, pick a winner). Genre is attributed only on success; a miss has no
    /// single genre, so <c>ResolvedGenre</c> is null. Preserved for rollback; not registered by default.
    /// </summary>
    public sealed class LegacyShelfPassiveResolver : IPassivePurchaseResolver
    {
        private readonly IPassiveSaleSelector _selector;

        public LegacyShelfPassiveResolver(IPassiveSaleSelector selector)
        {
            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
        }

        public PassiveAttemptResult Resolve(Customer self, CustomerContext ctx, IReadOnlyList<ShelfBook> available)
        {
            var candidate = _selector.PickPassiveSale(available, ctx.Location, ctx.ActiveDecorIds, ctx.Random);
            if (candidate == null)
                return PassiveAttemptResult.Miss(null);

            var genre = candidate.MatchedGenres is { Count: > 0 } ? candidate.MatchedGenres[0] : null;
            return PassiveAttemptResult.Hit(genre, candidate.Book, candidate.MatchedGenres, candidate.MatchedTags);
        }
    }
}
