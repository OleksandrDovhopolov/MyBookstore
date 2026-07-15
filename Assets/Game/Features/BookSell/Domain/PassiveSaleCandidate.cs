using System;
using System.Collections.Generic;

namespace Book.Sell.Domain
{
    /// <summary>
    /// Result returned by <see cref="Services.IPassiveSaleSelector"/>: which book to sell
    /// passively + which location demand items actually matched. The matched lists let the
    /// View show context like "passive sale: ... demand: study, sci-fi".
    /// </summary>
    public sealed class PassiveSaleCandidate
    {
        public ShelfBook Book { get; }
        public IReadOnlyList<string> MatchedGenres { get; }
        public IReadOnlyList<string> MatchedQualities { get; }

        public PassiveSaleCandidate(
            ShelfBook book,
            IReadOnlyList<string> matchedGenres,
            IReadOnlyList<string> matchedQualities)
        {
            Book = book;
            MatchedGenres = matchedGenres ?? Array.Empty<string>();
            MatchedQualities = matchedQualities ?? Array.Empty<string>();
        }
    }
}
