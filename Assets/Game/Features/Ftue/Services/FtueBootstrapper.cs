using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.DayCycle.Day;
using Game.Ftue.Domain;
using Game.Inventory.API;
using Save;
using UnityEngine;

namespace Game.Ftue.Services
{
    /// <inheritdoc cref="IFtueBootstrapper"/>
    public sealed class FtueBootstrapper : IFtueBootstrapper
    {
        private const string LogPrefix = "[FTUE]";

        // MVP: starter preset is hardcoded. Tiny Bookshop reference: 60 gold + 27 books across genres.
        // Migration to economy.json / ftue.json is a separate task (paired with the DailyBookSlots refactor).
        // Tracked in docs/INPROGRESS/CORE_LOOP_STATUS.md under "Known limitations".
        private const int StartingGold = 60;

        // Genre order is fixed so the resulting inventory seeding order is stable across runs (good for tests).
        private static readonly IReadOnlyList<KeyValuePair<string, int>> PresetCounts = new[]
        {
            new KeyValuePair<string, int>("Fantasy", 5),
            new KeyValuePair<string, int>("Crime",   5),
            new KeyValuePair<string, int>("Drama",   6),
            new KeyValuePair<string, int>("Classic", 3),
            new KeyValuePair<string, int>("Fact",    3),
            new KeyValuePair<string, int>("Travel",  3),
            new KeyValuePair<string, int>("Kids",    2)
        };

        private readonly ISaveService _save;
        private readonly IConfigsService _configs;
        private readonly IInventoryService _inventory;

        public FtueBootstrapper(ISaveService save, IConfigsService configs, IInventoryService inventory)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        }

        public async UniTask RunAsync(CancellationToken ct)
        {
            var applied = await _save.GetModuleAsync<FtueAppliedState>(FtueSaveKeys.Applied, ct);
            if (applied != null && applied.Applied)
            {
                Debug.Log($"{LogPrefix} skip — already applied at {applied.AppliedAtUtcIso}.");
                return;
            }

            var existingDay = await _save.GetModuleAsync<DayProgressState>(DayProgressService.ModuleKey, ct);

            // Legacy save: the player has already progressed, but the ftue.applied marker is missing.
            // Just mark applied and leave their progress untouched. Covers migration of existing players.
            if (existingDay != null && (existingDay.CurrentDay > 1 || existingDay.CompletedDays.Count > 0))
            {
                Debug.Log($"{LogPrefix} skip — existing progress detected (day={existingDay.CurrentDay}), marking applied.");
                await WriteAppliedMarkerAsync(ct);
                return;
            }

            // Clean first launch: seed gold in day_progress, seed books in inventory.
            var state = existingDay ?? new DayProgressState();
            state.Gold = StartingGold;
            await _save.UpdateModuleAsync(DayProgressService.ModuleKey, state, DayProgressService.SchemaVersion, ct);

            var preset = BuildPresetBookIds()
                .Select(id => new InventoryItem(id, InventoryCategories.Book, count: 1))
                .ToList();
            await _inventory.AddBatchAsync(preset, ct);

            await WriteAppliedMarkerAsync(ct);

            Debug.Log($"{LogPrefix} applied: gold={state.Gold}, books={preset.Count}.");
        }

        private List<string> BuildPresetBookIds()
        {
            var catalog = _configs.GetAll<BookConfig>();
            if (catalog == null || catalog.Count == 0)
            {
                Debug.LogWarning($"{LogPrefix} BookConfig catalog is empty — preset will be empty.");
                return new List<string>();
            }

            var byGenre = catalog
                .Where(b => !string.IsNullOrEmpty(b?.Genre))
                .GroupBy(b => b.Genre)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<BookConfig>)g.ToList());

            var result = new List<string>(PresetCounts.Sum(p => p.Value));
            foreach (var (genre, requested) in PresetCounts)
            {
                if (!byGenre.TryGetValue(genre, out var genreBooks) || genreBooks.Count == 0)
                {
                    Debug.LogWarning($"{LogPrefix} genre '{genre}' missing from catalog — skipping {requested} books.");
                    continue;
                }

                var picked = genreBooks
                    .OrderByDescending(b => b.RarityWeight)
                    .ThenBy(b => b.Id, StringComparer.Ordinal)
                    .Take(requested)
                    .ToList();

                if (picked.Count < requested)
                    Debug.LogWarning($"{LogPrefix} genre '{genre}': catalog has {picked.Count} books, requested {requested} — took {picked.Count}.");

                foreach (var book in picked)
                    result.Add(book.Id);
            }

            return result;
        }

        private UniTask WriteAppliedMarkerAsync(CancellationToken ct)
        {
            var marker = new FtueAppliedState
            {
                Applied = true,
                AppliedAtUtcIso = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture)
            };
            return _save.UpdateModuleAsync(FtueSaveKeys.Applied, marker, FtueSaveKeys.AppliedSchemaVersion, ct);
        }
    }
}
