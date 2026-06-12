using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.DayCycle.Day;
using Game.Ftue.Domain;
using Save;
using UnityEngine;

namespace Game.Ftue.Services
{
    /// <inheritdoc cref="IFtueBootstrapper"/>
    public sealed class FtueBootstrapper : IFtueBootstrapper
    {
        private const string LogPrefix = "[FTUE]";

        // MVP: стартовый пресет захардкожен. По спеке TB-референс: 60 золота + 27 книг по жанрам.
        // Миграция в economy.json / ftue.json — отдельная задача (вместе с DailyBookSlots refactor).
        // Зафиксировано в docs/INPROGRESS/CORE_LOOP_STATUS.md → "Известные ограничения".
        private const int StartingGold = 60;

        // Порядок жанров фиксирован — он же определяет порядок ids в OwnedBookIds (для предсказуемости тестов).
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

        public FtueBootstrapper(ISaveService save, IConfigsService configs)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
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

            // Легаси-сейв: игрок уже играл, но маркер ftue.applied не было — просто помечаем,
            // не перезаписываем прогресс. Покрывает миграцию существующих игроков.
            if (existingDay != null && (existingDay.CurrentDay > 1 || existingDay.CompletedDays.Count > 0))
            {
                Debug.Log($"{LogPrefix} skip — existing progress detected (day={existingDay.CurrentDay}), marking applied.");
                await WriteAppliedMarkerAsync(ct);
                return;
            }

            // Чистый первый запуск.
            var state = existingDay ?? new DayProgressState();
            state.Gold = StartingGold;
            state.OwnedBookIds = BuildPresetBookIds();

            await _save.UpdateModuleAsync(DayProgressService.ModuleKey, state, DayProgressService.SchemaVersion, ct);
            await WriteAppliedMarkerAsync(ct);

            Debug.Log($"{LogPrefix} applied: gold={state.Gold}, books={state.OwnedBookIds.Count}.");
        }

        private List<string> BuildPresetBookIds()
        {
            var catalog = _configs.GetAll<BookConfig>();
            if (catalog == null || catalog.Count == 0)
            {
                Debug.LogWarning($"{LogPrefix} BookConfig каталог пуст — пресет будет пустым.");
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
                    Debug.LogWarning($"{LogPrefix} жанр '{genre}' отсутствует в каталоге — пропуск {requested} книг.");
                    continue;
                }

                var picked = genreBooks
                    .OrderByDescending(b => b.RarityWeight)
                    .ThenBy(b => b.Id, StringComparer.Ordinal)
                    .Take(requested)
                    .ToList();

                if (picked.Count < requested)
                    Debug.LogWarning($"{LogPrefix} жанр '{genre}': в каталоге {picked.Count} книг, запрошено {requested} — взято {picked.Count}.");

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
