using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    /// <inheritdoc cref="IPassiveSaleSelector"/>
    public sealed class DefaultPassiveSaleSelector : IPassiveSaleSelector
    {
        public PassiveSaleCandidate PickPassiveSale(
            IReadOnlyList<ShelfBook> shelf,
            LocationConfig location,
            ISalesRandom random)
        {
            if (shelf == null || shelf.Count == 0 || location == null || random == null)
                return null;

            // Collect candidates together with the demand items they matched.
            var candidates = new List<(ShelfBook book, List<string> genres, List<string> tags)>(shelf.Count);
            for (var i = 0; i < shelf.Count; i++)
            {
                var book = shelf[i];
                if (book.State != ShelfBookState.Available) continue;

                var matchedGenres = CollectGenreMatches(book.Config.Genre, location.DemandGenres);
                var matchedTags = CollectTagMatches(book.Config.Tags, location.DemandTags);

                if (matchedGenres.Count > 0 || matchedTags.Count > 0)
                    candidates.Add((book, matchedGenres, matchedTags));
            }

            if (candidates.Count == 0) return null;

            var index = random.Range(0, candidates.Count);
            var picked = candidates[index];
            return new PassiveSaleCandidate(picked.book, picked.genres, picked.tags);
        }

        private static List<string> CollectGenreMatches(string bookGenre, string[] demandGenres)
        {
            var matched = new List<string>();
            if (demandGenres == null || string.IsNullOrEmpty(bookGenre)) return matched;
            foreach (var g in demandGenres)
            {
                if (string.IsNullOrEmpty(g)) continue;
                if (string.Equals(g, bookGenre, StringComparison.OrdinalIgnoreCase))
                {
                    matched.Add(g);
                    break;  // a book has exactly one Genre — at most one match
                }
            }
            return matched;
        }

        private static List<string> CollectTagMatches(string[] bookTags, string[] demandTags)
        {
            var matched = new List<string>();
            if (demandTags == null || bookTags == null) return matched;
            foreach (var d in demandTags)
            {
                if (string.IsNullOrEmpty(d)) continue;
                foreach (var b in bookTags)
                {
                    if (string.IsNullOrEmpty(b)) continue;
                    if (string.Equals(d, b, StringComparison.OrdinalIgnoreCase))
                    {
                        matched.Add(d);
                        break;
                    }
                }
            }
            return matched;
        }
    }
}
