using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Book.Sell.Services;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Game.DayCycle.Day;
using Game.Preparation.Domain;
using Save;
using UnityEngine;

namespace Game.Preparation.Services
{
    /// <inheritdoc cref="IPreparationSessionService"/>
    public sealed class PreparationSessionService : IPreparationSessionService
    {
        private const string LogPrefix = "[Preparation.Session]";

        // MVP: capacity захардкожена временно (см. CORE_LOOP_STATUS «Известные ограничения»).
        private const int DefaultMinDailyBooks = 0;
        private const int DefaultDailyBookSlots = 12;
        private const string DefaultLocationId = "loc_downtown";

        private readonly ISaveService _save;
        private readonly IDayProgressService _dayProgress;
        private readonly IPreparationInventoryProvider _inventory;
        private readonly ISalesShelfStateService _shelfState;

        private PreparationSessionState _state;

        // Кэш на сессию: непроданные книги игрока, сгруппированные по жанру (потолок квот + пул для резолва).
        private readonly Dictionary<string, List<BookConfig>> _availableByGenre = new(StringComparer.OrdinalIgnoreCase);

        public PreparationSessionService(
            ISaveService save,
            IDayProgressService dayProgress,
            IPreparationInventoryProvider inventory,
            ISalesShelfStateService shelfState)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _dayProgress = dayProgress ?? throw new ArgumentNullException(nameof(dayProgress));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _shelfState = shelfState ?? throw new ArgumentNullException(nameof(shelfState));

            Capacity = new PreparationCapacity(DefaultMinDailyBooks, DefaultDailyBookSlots);
        }

        public PreparationCapacity Capacity { get; }

        public PreparationSessionState CurrentState => _state;

        public event Action<PreparationSessionState> StateChanged;

        public int TotalSelected => _state?.SelectedBookIds?.Count ?? 0;

        public UniTask<IReadOnlyList<GenreSelectionItem>> StartOrResumeAsync(CancellationToken ct)
            => StartOrResumeCoreAsync(ct, setPreparationPhase: true);

        public async UniTask<IReadOnlyDictionary<string, int>> GetGenreQuantitiesPreviewAsync(CancellationToken ct)
        {
            await StartOrResumeCoreAsync(ct, setPreparationPhase: false);
            return _state?.GenreQuantities != null
                ? new Dictionary<string, int>(_state.GenreQuantities, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        private async UniTask<IReadOnlyList<GenreSelectionItem>> StartOrResumeCoreAsync(
            CancellationToken ct,
            bool setPreparationPhase)
        {
            CacheAvailableByGenre();

            var saved = await _save.GetModuleAsync<PreparationSessionState>(PreparationSaveKeys.Session, ct);
            var currentDay = _dayProgress.Current.CurrentDay;

            var needsFresh = saved == null || saved.Day != currentDay || saved.Confirmed;
            if (needsFresh)
            {
                _state = new PreparationSessionState
                {
                    Day = currentDay,
                    LocationId = DefaultLocationId,
                    GenreQuantities = SeedInitialQuantities(),
                    SelectedDecorIds = new List<string>(),
                    Confirmed = false
                };
            }
            else
            {
                _state = saved;
                _state.GenreQuantities ??= new Dictionary<string, int>();
                if (_state.GenreQuantities.Count == 0 && _state.SelectedBookIds is { Count: > 0 })
                    _state.GenreQuantities = BuildQuantitiesFromBookIds(_state.SelectedBookIds);
                ClampQuantitiesToAvailable(_state.GenreQuantities);
            }

            _state.SelectedBookIds = ResolveSelectedBookIds(_state.GenreQuantities);
            await PersistAsync(ct);

            if (setPreparationPhase && _dayProgress.Current.CurrentPhase != DayPhase.Preparation)
                await _dayProgress.SetPhaseAsync(DayPhase.Preparation, ct);

            return BuildGenreItems();
        }

        public async UniTask SetGenreQuantityAsync(string genre, int quantity, CancellationToken ct)
        {
            if (_state == null)
            {
                Debug.LogWarning($"{LogPrefix} SetGenreQuantityAsync вызван до StartOrResumeAsync.");
                return;
            }

            if (string.IsNullOrEmpty(genre) || !_availableByGenre.ContainsKey(genre)) return;

            var available = AvailableCount(genre);
            var otherTotal = _state.GenreQuantities
                .Where(kv => !string.Equals(kv.Key, genre, StringComparison.OrdinalIgnoreCase))
                .Sum(kv => kv.Value);
            var maxForThis = Mathf.Max(0, Capacity.DailyBookSlots - otherTotal);

            var clamped = Mathf.Clamp(quantity, 0, Mathf.Min(available, maxForThis));
            _state.GenreQuantities[genre] = clamped;

            _state.SelectedBookIds = ResolveSelectedBookIds(_state.GenreQuantities);
            await PersistAsync(ct);
            StateChanged?.Invoke(_state);
        }

        public async UniTask RandomizeAsync(CancellationToken ct)
        {
            if (_state == null) return;

            // Берём весь пул непроданных книг, перемешиваем, отрезаем по лимиту → квоты по жанрам.
            var pool = _availableByGenre.Values.SelectMany(b => b).ToList();
            Shuffle(pool);

            var take = Mathf.Min(Capacity.DailyBookSlots, pool.Count);
            var quantities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < take; i++)
            {
                var genre = pool[i].Genre;
                if (string.IsNullOrEmpty(genre)) continue;
                quantities.TryGetValue(genre, out var c);
                quantities[genre] = c + 1;
            }

            _state.GenreQuantities = quantities;
            _state.SelectedBookIds = ResolveSelectedBookIds(quantities);
            await PersistAsync(ct);
            StateChanged?.Invoke(_state);
        }

        public PreparationValidationResult Validate()
        {
            if (_state == null)
                return PreparationValidationResult.Fail("Session not started");

            if (Capacity.MinDailyBooks > 0 && TotalSelected < Capacity.MinDailyBooks)
                return PreparationValidationResult.Fail($"Выберите хотя бы {Capacity.MinDailyBooks} книгу");

            return PreparationValidationResult.Ok();
        }

        public async UniTask<bool> ConfirmAsync(CancellationToken ct)
        {
            var validation = Validate();
            if (!validation.IsValid)
            {
                Debug.LogWarning($"{LogPrefix} Confirm отклонён: {string.Join("; ", validation.Errors)}");
                return false;
            }

            _state.Confirmed = true;
            _state.SelectedBookIds = ResolveSelectedBookIds(_state.GenreQuantities);
            await _shelfState.SetShelfAsync(_state.SelectedBookIds, ct);
            await PersistAsync(ct);
            await _dayProgress.SetPhaseAsync(DayPhase.Sales, ct);

            Debug.Log($"{LogPrefix} day={_state.Day} location={_state.LocationId} shelf={_state.SelectedBookIds.Count} → Sales.");
            return true;
        }

        // ---------- internals ----------

        private void CacheAvailableByGenre()
        {
            _availableByGenre.Clear();
            // GetOwnedBooks() уже исключает проданные книги (DayProgressInventoryProvider).
            foreach (var book in _inventory.GetOwnedBooks())
            {
                if (book == null || string.IsNullOrEmpty(book.Genre)) continue;
                if (!_availableByGenre.TryGetValue(book.Genre, out var list))
                {
                    list = new List<BookConfig>();
                    _availableByGenre[book.Genre] = list;
                }
                list.Add(book);
            }
        }

        private int AvailableCount(string genre)
            => _availableByGenre.TryGetValue(genre, out var books) ? books.Count : 0;

        // Стартовые квоты = непроданные книги с прошлой полки, посчитанные по жанрам (continuity/restock).
        private Dictionary<string, int> SeedInitialQuantities()
        {
            var quantities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var genreById = BuildGenreById();

            foreach (var bookId in _shelfState.ShelfBookIds)
            {
                if (string.IsNullOrEmpty(bookId)) continue;
                if (!genreById.TryGetValue(bookId, out var genre)) continue; // продана/не во владении — пропускаем
                quantities.TryGetValue(genre, out var c);
                quantities[genre] = c + 1;
            }

            ClampQuantitiesToAvailable(quantities);
            return quantities;
        }

        private void ClampQuantitiesToAvailable(Dictionary<string, int> quantities)
        {
            // Клампим каждую квоту к доступному и убираем жанры без наличия.
            foreach (var genre in quantities.Keys.ToList())
            {
                var available = AvailableCount(genre);
                if (available <= 0) quantities.Remove(genre);
                else quantities[genre] = Mathf.Clamp(quantities[genre], 0, available);
            }
            TrimToCapacity(quantities);
        }

        private void TrimToCapacity(Dictionary<string, int> quantities)
        {
            var total = quantities.Values.Sum();
            while (total > Capacity.DailyBookSlots)
            {
                var genre = quantities.FirstOrDefault(kv => kv.Value > 0).Key;
                if (genre == null) break;
                quantities[genre] -= 1;
                total -= 1;
            }
        }

        private Dictionary<string, string> BuildGenreById()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in _availableByGenre)
                foreach (var book in kv.Value)
                    if (!string.IsNullOrEmpty(book.Id)) map[book.Id] = kv.Key;
            return map;
        }

        private Dictionary<string, int> BuildQuantitiesFromBookIds(IReadOnlyList<string> bookIds)
        {
            var quantities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (bookIds == null || bookIds.Count == 0) return quantities;

            var genreById = BuildGenreById();
            for (var i = 0; i < bookIds.Count; i++)
            {
                var bookId = bookIds[i];
                if (string.IsNullOrEmpty(bookId)) continue;
                if (!genreById.TryGetValue(bookId, out var genre)) continue;

                quantities.TryGetValue(genre, out var count);
                quantities[genre] = count + 1;
            }

            return quantities;
        }

        // Резолв квот в конкретные id: непроданные с прошлой полки первыми, затем добор случайно.
        private List<string> ResolveSelectedBookIds(IReadOnlyDictionary<string, int> quantities)
        {
            var result = new List<string>();
            if (quantities == null) return result;

            var priorSet = new HashSet<string>(_shelfState.ShelfBookIds ?? Array.Empty<string>(), StringComparer.Ordinal);

            foreach (var kv in quantities)
            {
                var genre = kv.Key;
                var qty = kv.Value;
                if (qty <= 0) continue;
                if (!_availableByGenre.TryGetValue(genre, out var books) || books.Count == 0) continue;

                var prior = books.Where(b => priorSet.Contains(b.Id)).ToList();
                var rest = books.Where(b => !priorSet.Contains(b.Id)).ToList();
                Shuffle(rest);

                var take = Mathf.Min(qty, books.Count);
                foreach (var book in prior.Concat(rest).Take(take))
                    result.Add(book.Id);
            }

            return result;
        }

        private IReadOnlyList<GenreSelectionItem> BuildGenreItems()
        {
            return _availableByGenre
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => new GenreSelectionItem(
                    kv.Key,
                    kv.Value.Count,
                    _state.GenreQuantities.TryGetValue(kv.Key, out var q) ? q : 0))
                .ToList();
        }

        private UniTask PersistAsync(CancellationToken ct)
            => _save.UpdateModuleAsync(PreparationSaveKeys.Session, _state, PreparationSaveKeys.SessionSchemaVersion, ct);

        private static void Shuffle<T>(IList<T> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
