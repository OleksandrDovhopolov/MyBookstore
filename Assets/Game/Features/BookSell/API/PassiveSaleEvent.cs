using System;
using System.Collections.Generic;

namespace Book.Sell.API
{
    /// <summary>
    /// A background ("passive") sale: a book left the shelf on its own because it matched
    /// the location's demand. Carries the demand items that triggered the sale so the UI
    /// can show "why this book sold".
    /// </summary>
    public sealed class PassiveSaleEvent
    {
        public string BookId { get; }
        public int GoldEarned { get; }

        /// <summary>Genres from LocationConfig.DemandGenres that matched the sold book. Empty if none.</summary>
        public IReadOnlyList<string> MatchedGenres { get; }

        /// <summary>Legacy demand matches. Passive sales keep this empty; active quality matching lives elsewhere.</summary>
        public IReadOnlyList<string> MatchedQualities { get; }

        public PassiveSaleEvent(
            string bookId,
            int goldEarned,
            IReadOnlyList<string> matchedGenres = null,
            IReadOnlyList<string> matchedQualities = null)
        {
            BookId = bookId;
            GoldEarned = goldEarned;
            MatchedGenres = matchedGenres ?? Array.Empty<string>();
            MatchedQualities = matchedQualities ?? Array.Empty<string>();
        }
    }
}
