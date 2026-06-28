using System;
using System.Collections.Generic;
using Game.Configs.Models;

namespace Game.Rewards.Services
{
    /// <summary>
    /// Per-box selection rules for <see cref="BookBoxRewardExpander"/>. Each rule consists of:
    /// - <c>Filter</c>: which books are eligible.
    /// - <c>Weight</c>: relative odds for each candidate during weighted-without-replacement sampling.
    /// - <c>Rolls</c>: how many books to roll.
    /// </summary>
    /// <remarks>
    /// Hardcoded in Phase 0. Balance is intentionally rough — see <c>SHOP.md §11</c> for the open
    /// "config-driven rules" question that will land in Phase 1 alongside <c>reward_specs.json</c>.
    /// </remarks>
    internal static class BookBoxPoolRules
    {
        public sealed class Rule
        {
            public Predicate<BookConfig> Filter { get; }
            public Func<BookConfig, double> Weight { get; }
            public int Rolls { get; }

            public Rule(Predicate<BookConfig> filter, Func<BookConfig, double> weight, int rolls)
            {
                Filter = filter;
                Weight = weight;
                Rolls = rolls;
            }
        }

        // Hardcoded rules keyed by RewardSpec.Id (same id as the shop lot's rewardId).
        // book_box_common_15:        15 books, any genre. Lower RarityWeight = higher chance.
        // book_box_rare_8:           8 rare books (RarityWeight >= 0.6). Higher RarityWeight = higher chance.
        // book_box_genre_dystopic_1: 1 book in Fantasy with "dark" mood. Weighted by RarityWeight.
        // book_box_genre_heartfelt_1: 1 book in Drama with "romantic" mood. Weighted by RarityWeight.
        private static readonly IReadOnlyDictionary<string, Rule> _rules = new Dictionary<string, Rule>
        {
            ["book_box_common_15"] = new Rule(
                filter: _ => true,
                weight: b => Math.Max(0.0001, 1.0 - b.RarityWeight),
                rolls: 15),

            ["book_box_rare_8"] = new Rule(
                filter: b => b.RarityWeight >= 0.6f,
                weight: b => b.RarityWeight,
                rolls: 8),

            ["book_box_genre_dystopic_1"] = new Rule(
                filter: b => string.Equals(b.Genre, "Fantasy", StringComparison.OrdinalIgnoreCase)
                             && HasMood(b, "dark"),
                weight: b => b.RarityWeight,
                rolls: 1),

            ["book_box_genre_heartfelt_1"] = new Rule(
                filter: b => string.Equals(b.Genre, "Drama", StringComparison.OrdinalIgnoreCase)
                             && HasMood(b, "romantic"),
                weight: b => b.RarityWeight,
                rolls: 1),
        };

        public static bool TryGet(string boxId, out Rule rule) => _rules.TryGetValue(boxId, out rule);

        public static bool IsBookBoxId(string specId) =>
            !string.IsNullOrEmpty(specId) && specId.StartsWith("book_box_", StringComparison.Ordinal);

        private static bool HasMood(BookConfig b, string mood)
        {
            if (b.Mood == null) return false;
            for (var i = 0; i < b.Mood.Length; i++)
                if (string.Equals(b.Mood[i], mood, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
