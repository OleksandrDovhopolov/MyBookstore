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

        /// <summary>Tags from LocationConfig.DemandTags that matched the sold book. Empty if none.</summary>
        public IReadOnlyList<string> MatchedTags { get; }

        public PassiveSaleEvent(
            string bookId,
            int goldEarned,
            IReadOnlyList<string> matchedGenres = null,
            IReadOnlyList<string> matchedTags = null)
        {
            BookId = bookId;
            GoldEarned = goldEarned;
            MatchedGenres = matchedGenres ?? Array.Empty<string>();
            MatchedTags = matchedTags ?? Array.Empty<string>();
        }
    }
}
