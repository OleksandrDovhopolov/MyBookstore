using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using Game.Rewards.API;
using UnityEngine;
// PR5: filters pool by inventory ownership to avoid silent dupe-drop in book category (Unique mode).

namespace Game.Rewards.Services
{
    /// <summary>
    /// Expands <c>book_box_*</c> reward specs into concrete book <see cref="RewardItem"/>s by rolling
    /// from <see cref="BookConfig"/> pools according to <see cref="BookBoxPoolRules"/>. Sampling is
    /// weighted-without-replacement: a player who pays for "15 books" gets up to 15 distinct ids
    /// (the pool may be smaller; in that case we return however many it has).
    /// </summary>
    /// <remarks>
    /// Phase 0 only. Phase 2+ replaces this with server-side rolling — the shop config keeps
    /// <c>rewardItems</c> empty for book-box lots, the expander chain becomes a no-op once the server
    /// returns already-expanded specs.
    /// </remarks>
    public sealed class BookBoxRewardExpander : IRewardSpecExpander
    {
        private const string LogPrefix = "[BookBox]";

        private readonly IConfigsService _configs;
        private readonly IInventoryService _inventory;
        private readonly IRewardRandom _random;

        public BookBoxRewardExpander(IConfigsService configs, IInventoryService inventory, IRewardRandom random)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public bool CanExpand(RewardSpec spec) =>
            spec != null && BookBoxPoolRules.IsBookBoxId(spec.Id);

        public UniTask<RewardSpec> ExpandAsync(RewardSpec spec, CancellationToken ct)
        {
            if (!BookBoxPoolRules.TryGet(spec.Id, out var rule))
            {
                Debug.LogWarning($"{LogPrefix} Unknown book-box id '{spec.Id}'. Returning empty spec.");
                return UniTask.FromResult(new RewardSpec(spec.Id, Array.Empty<RewardItem>()));
            }

            var pool = BuildPool(rule.Filter, out var diagnostics);
            if (pool.Count == 0)
            {
                Debug.LogError($"{LogPrefix} {DescribeEmptyPool(spec.Id, rule, diagnostics)}");
                return UniTask.FromResult(new RewardSpec(spec.Id, Array.Empty<RewardItem>()));
            }

            var rolls = Math.Min(rule.Rolls, pool.Count);
            var weights = BuildWeights(pool, rule.Weight);
            var items = new RewardItem[rolls];

            for (var i = 0; i < rolls; i++)
            {
                var pickIndex = WeightedPick(weights);
                items[i] = RewardItem.InventoryItem(pool[pickIndex].Id, InventoryCategories.Book, 1);

                // Without replacement: remove picked entry from both lists.
                pool.RemoveAt(pickIndex);
                weights.RemoveAt(pickIndex);
            }

            if (rolls < rule.Rolls)
                Debug.LogWarning(
                    $"{LogPrefix} Underfilled box '{spec.Id}': delivered {rolls}/{rule.Rolls} books. " +
                    $"Catalog has {diagnostics.Total} book(s), {diagnostics.MatchedFilter} match the box rule " +
                    $"({rule.FilterDescription}), and {diagnostics.OwnedSkipped} of those are already owned " +
                    $"(books are Unique, so owned ids are never rolled twice).");

            return UniTask.FromResult(new RewardSpec(spec.Id, items));
        }

        /// <summary>Counts behind a pool, kept so an empty/underfilled box can say *why* it is empty.</summary>
        private readonly struct PoolDiagnostics
        {
            public int Total { get; }           // books in the catalog
            public int Invalid { get; }         // null / no id
            public int MatchedFilter { get; }   // passed the box rule, before the ownership cut
            public int OwnedSkipped { get; }    // matched the rule but the player already owns them

            public PoolDiagnostics(int total, int invalid, int matchedFilter, int ownedSkipped)
            {
                Total = total;
                Invalid = invalid;
                MatchedFilter = matchedFilter;
                OwnedSkipped = ownedSkipped;
            }
        }

        /// <summary>
        /// Spells out an empty pool: the two causes need opposite fixes. "No book matches the rule" is a
        /// content bug that ships broken (e.g. a catalog with no <c>rarityWeight</c> leaves every book at the
        /// 0.5 default, so a <c>RarityWeight &gt;= 0.6</c> box can never fill) and is caught before the build
        /// by <c>BookBoxPoolValidator</c>. "All matching books already owned" is a legitimate end-state of a
        /// nearly complete collection and needs a shop/limit change, not a data fix.
        /// </summary>
        private static string DescribeEmptyPool(string boxId, BookBoxPoolRules.Rule rule, PoolDiagnostics d)
        {
            var head =
                $"Box '{boxId}' rolled nothing — the player paid and received no books. " +
                $"Rule: {rule.FilterDescription}; rolls {rule.Rolls}. " +
                $"Catalog: {d.Total} book(s)" + (d.Invalid > 0 ? $" ({d.Invalid} skipped as null/no-id)" : "") + ".";

            if (d.MatchedFilter == 0)
                return head +
                       " CAUSE: no book in the catalog matches the rule, so this box can never fill — " +
                       "a content bug, not a play state. Check that the fields the rule reads are actually " +
                       "present in the books config (a missing field silently falls back to its C# default).";

            return head +
                   $" CAUSE: {d.MatchedFilter} book(s) match the rule but the player already owns all of them " +
                   "(books are Unique and are never rolled twice). The pool is exhausted, not misconfigured.";
        }

        private List<BookConfig> BuildPool(Predicate<BookConfig> filter, out PoolDiagnostics diagnostics)
        {
            var all = _configs.GetAll<BookConfig>();
            var pool = new List<BookConfig>(all.Count);
            var invalid = 0;
            var matchedFilter = 0;
            var ownedSkipped = 0;

            for (var i = 0; i < all.Count; i++)
            {
                var book = all[i];
                if (book == null || string.IsNullOrEmpty(book.Id))
                {
                    invalid++;
                    continue;
                }

                if (!filter(book)) continue;
                matchedFilter++;

                if (_inventory.Has(book.Id))   // PR5: skip books player already owns (book category is Unique).
                {
                    ownedSkipped++;
                    continue;
                }

                pool.Add(book);
            }

            diagnostics = new PoolDiagnostics(all.Count, invalid, matchedFilter, ownedSkipped);
            return pool;
        }

        private static List<double> BuildWeights(IReadOnlyList<BookConfig> pool, Func<BookConfig, double> weighter)
        {
            var weights = new List<double>(pool.Count);
            for (var i = 0; i < pool.Count; i++)
                weights.Add(Math.Max(0.0001, weighter(pool[i])));
            return weights;
        }

        private int WeightedPick(IReadOnlyList<double> weights)
        {
            var total = 0.0;
            for (var i = 0; i < weights.Count; i++) total += weights[i];

            var r = _random.NextDouble() * total;
            var acc = 0.0;
            for (var i = 0; i < weights.Count; i++)
            {
                acc += weights[i];
                if (r < acc) return i;
            }
            return weights.Count - 1; // floating-point safety net
        }
    }
}
