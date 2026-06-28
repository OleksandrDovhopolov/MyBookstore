using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using Game.Configs.Models;
using UnityEngine;

namespace Book.Sell.Services
{
    /// <summary>
    /// Two-stage probabilistic selector (ADR-0004).
    ///
    /// Stage 1: for each genre present on the shelf, compute the gate chance via
    /// <see cref="IBaseSaleChanceCalculator"/> and roll. Collect winners.
    /// Stage 2: pick one winner genre uniformly, then pick a concrete book within that genre
    /// using a cumulative-sum walk weighted by <see cref="BookConfig.RarityWeight"/>.
    ///
    /// Passive sales intentionally ignore tags/mood — those belong to the active mini-game.
    /// </summary>
    public sealed class WeightedPassiveSaleSelector : IPassiveSaleSelector
    {
        private const string LogPrefix = "[Sales.Passive]";

        // Floor for books whose authored weight is non-positive so they remain reachable.
        private const float RarityWeightFloor = 1e-4f;

        private readonly IBaseSaleChanceCalculator _calculator;

        public WeightedPassiveSaleSelector(IBaseSaleChanceCalculator calculator)
        {
            _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        }

        public PassiveSaleCandidate PickPassiveSale(
            IReadOnlyList<ShelfBook> shelf,
            LocationConfig location,
            IReadOnlyList<string> activeDecorIds,
            ISalesRandom random)
        {
            if (shelf == null || shelf.Count == 0 || random == null) return null;

            var groups = GroupByGenre(shelf);
            if (groups.Count == 0) return null;

            // Stage 1: per-genre gate. One dice roll per genre present on the shelf.
            var winners = new List<KeyValuePair<string, List<ShelfBook>>>();
            foreach (var kv in groups)
            {
                var copies = kv.Value.Count;
                var chance = _calculator.Compute(kv.Key, copies, location, activeDecorIds);
                if (chance <= 0d)
                {
                    Debug.Log($"{LogPrefix}   gate genre={kv.Key} copies={copies} chance=0.000 → SKIP (no chance)");
                    continue;
                }

                // NextDouble() is consumed only when chance > 0 — keep that order so seeds stay stable.
                var roll = random.NextDouble();
                var hit = roll < chance;
                Debug.Log($"{LogPrefix}   gate genre={kv.Key} copies={copies} chance={chance:F3} roll={roll:F3} → {(hit ? "HIT" : "MISS")}");
                if (hit) winners.Add(kv);
            }

            if (winners.Count == 0)
            {
                Debug.Log($"{LogPrefix} no genre passed the gate → passive MISS");
                return null;
            }

            // Stage 2a: uniformly choose one winning genre.
            var pickedIdx = winners.Count == 1 ? 0 : random.Range(0, winners.Count);
            var pickedGenre = winners[pickedIdx].Key;
            var genreBooks = winners[pickedIdx].Value;

            // Stage 2b: weighted pick by RarityWeight.
            var book = WeightedPick(genreBooks, random);
            if (book == null)
            {
                Debug.Log($"{LogPrefix} winner genre={pickedGenre} but no book could be picked → passive MISS");
                return null;
            }

            Debug.Log($"{LogPrefix} winner genre={pickedGenre} ({winners.Count} genre(s) passed) → book={book.BookId} \"{book.Config?.Title}\" rarity={EffectiveWeight(book):F3} price={book.Config?.BasePrice}");

            return new PassiveSaleCandidate(
                book,
                matchedGenres: new[] { pickedGenre },
                matchedTags: Array.Empty<string>());
        }

        private static Dictionary<string, List<ShelfBook>> GroupByGenre(IReadOnlyList<ShelfBook> shelf)
        {
            var groups = new Dictionary<string, List<ShelfBook>>(StringComparer.OrdinalIgnoreCase);
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

        private static ShelfBook WeightedPick(List<ShelfBook> books, ISalesRandom random)
        {
            if (books == null || books.Count == 0) return null;
            if (books.Count == 1) return books[0];

            double total = 0d;
            for (var i = 0; i < books.Count; i++)
                total += EffectiveWeight(books[i]);

            // Defensive: if every book has zero/negative weight (shouldn't happen with floor), pick uniformly.
            if (total <= 0d) return books[random.Range(0, books.Count)];

            var roll = random.NextDouble() * total;
            double cumulative = 0d;
            for (var i = 0; i < books.Count; i++)
            {
                cumulative += EffectiveWeight(books[i]);
                if (roll < cumulative) return books[i];
            }

            // Rounding fallback.
            return books[books.Count - 1];
        }

        private static double EffectiveWeight(ShelfBook book)
        {
            var w = book?.Config?.RarityWeight ?? 0f;
            return w > 0f ? w : RarityWeightFloor;
        }
    }
}
