using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using Game.Rewards.API;
using UnityEngine;

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
        private readonly IRewardRandom _random;

        public BookBoxRewardExpander(IConfigsService configs, IRewardRandom random)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
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

            var pool = BuildPool(rule.Filter);
            if (pool.Count == 0)
            {
                Debug.LogWarning($"{LogPrefix} Empty pool for '{spec.Id}'. Returning empty spec.");
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

            return UniTask.FromResult(new RewardSpec(spec.Id, items));
        }

        private List<BookConfig> BuildPool(Predicate<BookConfig> filter)
        {
            var all = _configs.GetAll<BookConfig>();
            var pool = new List<BookConfig>(all.Count);
            for (var i = 0; i < all.Count; i++)
            {
                var book = all[i];
                if (book != null && !string.IsNullOrEmpty(book.Id) && filter(book))
                    pool.Add(book);
            }
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
