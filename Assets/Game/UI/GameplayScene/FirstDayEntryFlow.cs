using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using Game.Configs;
using Game.Configs.Models;
using Game.DayCycle.Morning;
using Game.LocationUnlock.API;
using Game.Preparation.Services;
using UnityEngine;

namespace GameplayUI
{
    /// <summary>
    /// Day-1 "drop straight into the location" orchestrator (see docs/FTUE.md). Headless replay of what
    /// <c>PreparationWindow.ConfirmAsync</c> does, minus the UI: resolve the fixed day-1 location,
    /// auto-stock the shelf from the seeded inventory, then additively load the location scene.
    ///
    /// Not a DI service: constructed by <see cref="MainSceneBootstrap"/> from its injected dependencies,
    /// which keeps the installers assembly free of a reference to this UI assembly.
    ///
    /// Day-1 entry is free — unlike <c>PreparationWindow</c>, this flow does not charge the location entry fee.
    /// </summary>
    public sealed class FirstDayEntryFlow
    {
        private const string LogPrefix = "[FirstDayEntry]";

        private readonly IMorningSessionService _morning;
        private readonly IPreparationSessionService _preparation;
        private readonly IPreparationInventoryProvider _inventory;
        private readonly IGameFlowService _gameFlow;
        private readonly IConfigsService _configs;
        private readonly ILocationUnlockService _locationUnlock;
        private static readonly IReadOnlyList<string> FirstDayGenreOrder = new[]
        {
            "Fact",
            "Travel",
            "Fantasy",
            "Crime",
            "Drama",
            "Classic",
            "Kids"
        };

        public FirstDayEntryFlow(
            IMorningSessionService morning,
            IPreparationSessionService preparation,
            IGameFlowService gameFlow,
            IConfigsService configs,
            ILocationUnlockService locationUnlock = null,
            IPreparationInventoryProvider inventory = null)
        {
            _morning = morning ?? throw new ArgumentNullException(nameof(morning));
            _preparation = preparation ?? throw new ArgumentNullException(nameof(preparation));
            _gameFlow = gameFlow ?? throw new ArgumentNullException(nameof(gameFlow));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _inventory = inventory;
            _locationUnlock = locationUnlock; // optional-safe: null → treat all locations as unlocked
        }

        /// <summary>
        /// Runs the direct entry. Returns true if the player was taken into the location; false if any
        /// precondition failed (no location, day not continuable, empty stock) so the caller can fall
        /// back to the hub.
        /// </summary>
        public async UniTask<bool> EnterAsync(CancellationToken ct)
        {
            var locationId = ResolveFixedLocationId();
            if (string.IsNullOrEmpty(locationId))
            {
                Debug.LogError($"{LogPrefix} no unlocked location found — falling back to hub.");
                return false;
            }

            // Establish the day context and move Morning → Preparation (mirrors GameplaySceneController.StartGameAsync).
            var continueResult = await _morning.ContinueToPreparationAsync(ct);
            if (continueResult == null)
            {
                Debug.LogWarning($"{LogPrefix} morning session cannot continue — falling back to hub.");
                return false;
            }

            // Auto-stock: seed the session for this location, fill the shelf from the seeded inventory, confirm.
            await _preparation.StartOrResumeAsync(ct, locationId);
            await _preparation.SetSelectedBookIdsAsync(BuildFirstDayShelfPreset(), ct);

            var confirmed = await _preparation.ConfirmAsync(ct); // writes shelf state + advances to Sales
            if (!confirmed)
            {
                Debug.LogWarning($"{LogPrefix} preparation confirm rejected — falling back to hub.");
                return false;
            }

            // Additively load the location over the hub. Raises LocationLoadedChanged for the day-one tutorial.
            await _gameFlow.EnterLocationAsync(locationId, ct);

            Debug.Log($"{LogPrefix} entered location '{locationId}' directly with an auto-stocked shelf.");
            return true;
        }

        private IReadOnlyList<string> BuildFirstDayShelfPreset()
        {
            var capacity = _preparation.Capacity.DailyBookSlots;
            if (capacity <= 0) return Array.Empty<string>();

            var owned = (_inventory?.GetOwnedBooks() ?? Array.Empty<BookConfig>())
                .Where(b => b != null && !string.IsNullOrEmpty(b.Id) && !string.IsNullOrEmpty(b.PrimaryGenre))
                .GroupBy(b => b.Id, StringComparer.Ordinal)
                .Select(g => g.First())
                .ToList();

            var selected = new List<BookConfig>(capacity);
            var selectedIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var genre in CustomerScriptDayLookup.PassiveGenresForDay(_configs.GetAll<CustomerScriptConfig>(), 1))
                AddFirstByGenre(genre);

            // What is left of the shelf is split across genres proportionally to what the player owns.
            // Ordering the whole inventory by genre instead filled every slot from the head of
            // FirstDayGenreOrder and left the tail at zero: the FTUE seeds 54 books into 30 slots, so
            // Drama/Classic/Kids never reached the shelf on day 1.
            var pools = owned
                .Where(b => !selectedIds.Contains(b.Id))
                .GroupBy(b => b.PrimaryGenre, StringComparer.OrdinalIgnoreCase)
                .Select(g => (
                    Genre: g.Key,
                    Books: (IReadOnlyList<BookConfig>)g
                        .OrderByDescending(b => b.RarityWeight)
                        .ThenBy(b => b.Id, StringComparer.Ordinal)
                        .ToList()))
                .OrderBy(p => GenreRank(p.Genre))
                .ThenBy(p => p.Genre, StringComparer.Ordinal)
                .ToList();

            var quotas = Apportion(pools, capacity - selected.Count);
            for (var i = 0; i < pools.Count; i++)
                for (var j = 0; j < quotas[i]; j++)
                    Add(pools[i].Books[j]);

            // Rounding, or a pool that ran dry, can leave slots open — top them up in the canonical order.
            foreach (var book in owned.OrderBy(b => GenreRank(b.PrimaryGenre))
                         .ThenByDescending(b => b.RarityWeight)
                         .ThenBy(b => b.Id, StringComparer.Ordinal))
            {
                if (selected.Count >= capacity) break;
                Add(book);
            }

            return selected.Select(b => b.Id).ToList();

            void AddFirstByGenre(string genre)
            {
                if (selected.Count >= capacity) return;

                var book = owned
                    .Where(b => string.Equals(b.PrimaryGenre, genre, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(b => b.RarityWeight)
                    .ThenBy(b => b.Id, StringComparer.Ordinal)
                    .FirstOrDefault();
                Add(book);
            }

            void Add(BookConfig book)
            {
                if (book == null || selected.Count >= capacity) return;
                if (!selectedIds.Add(book.Id)) return;
                selected.Add(book);
            }

            int GenreRank(string genre)
            {
                for (var i = 0; i < FirstDayGenreOrder.Count; i++)
                    if (string.Equals(genre, FirstDayGenreOrder[i], StringComparison.OrdinalIgnoreCase))
                        return i;
                return FirstDayGenreOrder.Count;
            }
        }

        /// <summary>
        /// Splits <paramref name="slots"/> across the genre pools proportionally to their size, using the
        /// largest-remainder method. Pools arrive in FirstDayGenreOrder and LINQ ordering is stable, so
        /// equal fractions fall back to that order and the result is identical run to run.
        /// </summary>
        private static int[] Apportion(IReadOnlyList<(string Genre, IReadOnlyList<BookConfig> Books)> pools, int slots)
        {
            var quotas = new int[pools.Count];
            if (slots <= 0 || pools.Count == 0) return quotas;

            var available = new int[pools.Count];
            var total = 0;
            for (var i = 0; i < pools.Count; i++)
            {
                available[i] = pools[i].Books.Count;
                total += available[i];
            }

            // Everything the player owns fits — nothing to apportion.
            if (total <= slots)
            {
                Array.Copy(available, quotas, pools.Count);
                return quotas;
            }

            // Every owned genre takes one book before the rest is split, so no genre the player stocks shows
            // up as 0 on the shelf. With fewer slots than genres that promise cannot be kept and the
            // proportional pass alone decides who gets in.
            var left = slots;
            if (slots >= pools.Count)
            {
                for (var i = 0; i < pools.Count; i++)
                {
                    quotas[i] = 1;
                    available[i]--;
                    total--;
                    left--;
                }
            }

            var remainders = new double[pools.Count];
            var assigned = 0;
            for (var i = 0; i < pools.Count; i++)
            {
                var exact = total > 0 ? (double)available[i] * left / total : 0d;
                var floor = Mathf.Min((int)exact, available[i]);
                quotas[i] += floor;
                remainders[i] = exact - floor;
                assigned += floor;
            }

            // Hand out the rounding leftovers, largest fraction first; repeat because a pool can cap out
            // before it absorbs its share.
            var leftover = left - assigned;
            var order = Enumerable.Range(0, pools.Count)
                .OrderByDescending(i => remainders[i])
                .ToArray();

            while (leftover > 0)
            {
                var progressed = false;
                foreach (var i in order)
                {
                    if (leftover <= 0) break;
                    if (quotas[i] >= pools[i].Books.Count) continue;
                    quotas[i]++;
                    leftover--;
                    progressed = true;
                }

                if (!progressed) break;
            }

            return quotas;
        }

        // Day 1 = single fixed location: the first unlocked one in catalog order (mirror of
        // GameplaySceneController.PickFirstUnlockedLocationId).
        private string ResolveFixedLocationId()
        {
            foreach (var config in _configs.GetAll<LocationConfig>())
            {
                if (config == null || string.IsNullOrEmpty(config.Id)) continue;
                if (_locationUnlock == null || _locationUnlock.IsUnlocked(config.Id))
                    return config.Id;
            }

            return null;
        }
    }
}
