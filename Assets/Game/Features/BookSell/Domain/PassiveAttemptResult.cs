using System;
using System.Collections.Generic;

namespace Book.Sell.Domain
{
    /// <summary>
    /// Outcome of one passive purchase attempt. <see cref="ResolvedGenre"/> is the genre the attempt was
    /// made on — set even on a miss, so feedback can show it; the chosen genre lives here only (never in
    /// <see cref="MatchedGenres"/>). <see cref="Book"/> is set only on a hit.
    /// <see cref="MatchedGenres"/>/<see cref="MatchedTags"/> are the demand-match passthrough used to build
    /// <c>PassiveSaleEvent</c> (legacy model fills them; the requested-genre model leaves them empty).
    /// </summary>
    public sealed class PassiveAttemptResult
    {
        public string ResolvedGenre { get; }
        public bool Success { get; }
        public ShelfBook Book { get; }
        public IReadOnlyList<string> MatchedGenres { get; }
        public IReadOnlyList<string> MatchedTags { get; }

        private PassiveAttemptResult(
            string resolvedGenre, bool success, ShelfBook book,
            IReadOnlyList<string> matchedGenres, IReadOnlyList<string> matchedTags)
        {
            ResolvedGenre = resolvedGenre;
            Success = success;
            Book = book;
            MatchedGenres = matchedGenres ?? Array.Empty<string>();
            MatchedTags = matchedTags ?? Array.Empty<string>();
        }

        public static PassiveAttemptResult Hit(
            string genre, ShelfBook book,
            IReadOnlyList<string> matchedGenres = null, IReadOnlyList<string> matchedTags = null)
            => new(genre, true, book, matchedGenres, matchedTags);

        public static PassiveAttemptResult Miss(string genre)
            => new(genre, false, null, null, null);
    }
}
