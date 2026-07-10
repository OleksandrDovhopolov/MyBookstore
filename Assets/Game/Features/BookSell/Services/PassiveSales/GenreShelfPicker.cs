using System;
using System.Collections.Generic;
using Book.Sell.Domain;

namespace Book.Sell.Services
{
    /// <summary>
    /// Shared shelf helpers for passive resolvers: group available books by genre and weighted-pick a
    /// book within a genre by RarityWeight. The legacy <see cref="WeightedPassiveSaleSelector"/> keeps
    /// its own private copy — intentionally left untouched.
    /// </summary>
    public static class GenreShelfPicker
    {
        private const float RarityWeightFloor = 1e-4f;

        public static Dictionary<string, List<ShelfBook>> GroupAvailableByGenre(IReadOnlyList<ShelfBook> shelf)
        {
            var groups = new Dictionary<string, List<ShelfBook>>(StringComparer.OrdinalIgnoreCase);
            if (shelf == null) return groups;

            for (var i = 0; i < shelf.Count; i++)
            {
                var book = shelf[i];
                if (book == null || book.State != ShelfBookState.Available) continue;
                var genre = book.Config?.Genre;
                if (string.IsNullOrEmpty(genre)) continue;

                if (!groups.TryGetValue(genre, out var list))
                {
                    list = new List<ShelfBook>();
                    groups[genre] = list;
                }
                list.Add(book);
            }
            return groups;
        }

        public static ShelfBook WeightedPick(List<ShelfBook> books, ISalesRandom random)
        {
            if (books == null || books.Count == 0) return null;
            if (books.Count == 1) return books[0];

            double total = 0d;
            for (var i = 0; i < books.Count; i++) total += EffectiveWeight(books[i]);
            if (total <= 0d) return books[random.Range(0, books.Count)];

            var roll = random.NextDouble() * total;
            double cumulative = 0d;
            for (var i = 0; i < books.Count; i++)
            {
                cumulative += EffectiveWeight(books[i]);
                if (roll < cumulative) return books[i];
            }
            return books[books.Count - 1];
        }

        private static double EffectiveWeight(ShelfBook book)
        {
            var w = book?.Config?.RarityWeight ?? 0f;
            return w > 0f ? w : RarityWeightFloor;
        }
    }
}
