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
    /// - <c>FilterDescription</c>: the filter in words, so a runtime warning and the editor validator
    ///   explain an empty pool the same way.
    /// </summary>
    /// <remarks>
    /// Hardcoded in Phase 0. Balance is intentionally rough — see <c>SHOP.md §11</c> for the open
    /// "config-driven rules" question that will land in Phase 1 alongside <c>reward_specs.json</c>.
    /// <para>
    /// Public (not internal) so <c>BookBoxPoolValidator</c> in the editor assembly can replay the real
    /// predicates against the catalog instead of restating them — the same "reuse the runtime rule so the
    /// verdict cannot drift" approach <c>ActiveRequestValidator</c> takes with
    /// <c>BookConditionRequestEvaluator</c>.
    /// </para>
    /// </remarks>
    public static class BookBoxPoolRules
    {
        public const string BoxIdPrefix = "book_box_";

        public sealed class Rule
        {
            public Predicate<BookConfig> Filter { get; }
            public Func<BookConfig, double> Weight { get; }
            public int Rolls { get; }

            /// <summary>The filter in words, e.g. "RarityWeight &gt;= 0.6". Used in diagnostics only.</summary>
            public string FilterDescription { get; }

            public Rule(Predicate<BookConfig> filter, Func<BookConfig, double> weight, int rolls,
                string filterDescription)
            {
                Filter = filter;
                Weight = weight;
                Rolls = rolls;
                FilterDescription = filterDescription;
            }
        }

        // Hardcoded rules keyed by RewardSpec.Id (same id as the shop lot's rewardId).
        private static readonly Dictionary<string, Rule> _rules = new(StringComparer.Ordinal)
        {
            ["book_box_common_15"] = new Rule(
                filter: _ => true,
                weight: b => Math.Max(0.0001, 1.0 - b.RarityWeight),
                rolls: 15,
                filterDescription: "any book (lower RarityWeight = higher chance)"),

            ["book_box_rare_8"] = new Rule(
                filter: b => b.RarityWeight >= 0.6f,
                weight: b => b.RarityWeight,
                rolls: 8,
                filterDescription: "RarityWeight >= 0.6"),

            ["book_box_genre_dystopic_1"] = new Rule(
                filter: b => string.Equals(b.PrimaryGenre, "Fantasy", StringComparison.OrdinalIgnoreCase),
                weight: b => b.RarityWeight,
                rolls: 1,
                filterDescription: "PrimaryGenre == Fantasy"),

            ["book_box_genre_heartfelt_1"] = new Rule(
                filter: b => string.Equals(b.PrimaryGenre, "Drama", StringComparison.OrdinalIgnoreCase),
                weight: b => b.RarityWeight,
                rolls: 1,
                filterDescription: "PrimaryGenre == Drama"),
        };

        /// <summary>Every authored box id, in declaration order. Used by the editor validator.</summary>
        public static IEnumerable<KeyValuePair<string, Rule>> All => _rules;

        public static bool TryGet(string boxId, out Rule rule) => _rules.TryGetValue(boxId, out rule);

        public static bool IsBookBoxId(string specId) =>
            !string.IsNullOrEmpty(specId) && specId.StartsWith(BoxIdPrefix, StringComparison.Ordinal);
    }
}
