using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.SalesStats.API;
using Save;
using UnityEngine;

namespace Game.SalesStats.Services
{
    /// <summary>
    /// Default <see cref="ISalesStatsService"/> implementation. Self-registers as
    /// <see cref="ISaveHook"/> and loads counts on <see cref="AfterLoadAsync"/>, like
    /// <c>ProgressionService</c>.
    ///
    /// Batched-write policy (intentionally different from Progression/Shop write-through): a sales
    /// day can sell many books, so <see cref="RecordSold"/> only mutates an in-memory tally and flags
    /// the save dirty via <see cref="ISaveService.MarkDirty"/>. The actual persist happens once per
    /// save cycle in <see cref="BeforeSaveAsync"/> — no save-per-book I/O.
    /// </summary>
    public sealed class SalesStatsService : ISalesStatsService, ISalesStatsBaselineSource, ISaveHook
    {
        private const string LogPrefix = "[SalesStats]";

        private readonly ISaveService _save;
        private readonly ISalesStatsRepository _repository;
        private readonly IConfigsService _configs;

        // Keyed by genre config value (BookGenre.ToConfigValue()); case-insensitive for safety.
        private readonly Dictionary<string, int> _soldByGenre = new(StringComparer.OrdinalIgnoreCase);

        // locationId -> (genre config value -> count). Location keys are ordinal (config ids), genre keys
        // case-insensitive like _soldByGenre.
        private readonly Dictionary<string, Dictionary<string, int>> _soldByLocationGenre =
            new(StringComparer.Ordinal);

        // game day (1-based) -> (genre config value -> count).
        private readonly Dictionary<int, Dictionary<string, int>> _soldByDayGenre = new();

        private int _total;
        private bool _loaded;
        private bool _dirty;

        public SalesStatsService(ISaveService save, ISalesStatsRepository repository, IConfigsService configs)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            save.RegisterHook(this);
        }

        public event Action<SalesStatsChange> Changed;

        public int TotalSold => _total;

        public int GetSold(BookGenre genre)
            => _soldByGenre.TryGetValue(genre.ToConfigValue(), out var count) ? count : 0;

        public int GetSold(BookGenre genre, string locationId)
        {
            if (string.IsNullOrEmpty(locationId)) return 0;
            return _soldByLocationGenre.TryGetValue(locationId, out var byGenre)
                   && byGenre.TryGetValue(genre.ToConfigValue(), out var count)
                ? count
                : 0;
        }

        public int GetSoldOnDay(int day)
        {
            if (!_soldByDayGenre.TryGetValue(day, out var byGenre)) return 0;
            var sum = 0;
            foreach (var count in byGenre.Values) sum += count;
            return sum;
        }

        public int GetSoldOnDay(int day, BookGenre genre)
            => _soldByDayGenre.TryGetValue(day, out var byGenre)
               && byGenre.TryGetValue(genre.ToConfigValue(), out var count)
                ? count
                : 0;

        public int GetMaxSoldInSingleDay(BookGenre genre)
        {
            var key = genre.ToConfigValue();
            var max = 0;
            foreach (var byGenre in _soldByDayGenre.Values)
                if (byGenre.TryGetValue(key, out var count) && count > max)
                    max = count;
            return max;
        }

        // ----- ISalesStatsBaselineSource -----

        public SalesStatsStateDto CaptureBaseline() => BuildDto();

        public ISalesStatsReader CreateScopedReader(SalesStatsStateDto baseline)
            => new ScopedSalesStatsReader(this, baseline);

        // ----- ISaveHook -----

        public async UniTask AfterLoadAsync(CancellationToken ct)
        {
            var dto = await _repository.LoadAsync(ct);

            _soldByGenre.Clear();
            _soldByLocationGenre.Clear();
            _soldByDayGenre.Clear();
            _total = 0;

            // Normalize fills every known genre with 0 and drops unknown keys (BookGenreCounts).
            foreach (var pair in BookGenreCounts.Normalize(dto?.SoldByGenre))
            {
                _soldByGenre[pair.Key] = pair.Value;
                _total += pair.Value;
            }

            // v1 saves omit these maps (null) — migrating up is just an empty fill.
            if (dto?.SoldByLocationGenre != null)
                foreach (var location in dto.SoldByLocationGenre)
                {
                    if (string.IsNullOrEmpty(location.Key) || location.Value == null) continue;
                    _soldByLocationGenre[location.Key] =
                        new Dictionary<string, int>(location.Value, StringComparer.OrdinalIgnoreCase);
                }

            if (dto?.SoldByDayGenre != null)
                foreach (var day in dto.SoldByDayGenre)
                {
                    if (day.Value == null) continue;
                    _soldByDayGenre[day.Key] =
                        new Dictionary<string, int>(day.Value, StringComparer.OrdinalIgnoreCase);
                }

            _loaded = true;
            _dirty = false;
            Debug.Log($"{LogPrefix} loaded: total={_total}, locations={_soldByLocationGenre.Count}, days={_soldByDayGenre.Count}.");
        }

        public UniTask BeforeSaveAsync(CancellationToken ct)
        {
            // Batched flush: only writes when something actually changed since the last persist.
            if (!_dirty) return UniTask.CompletedTask;
            _dirty = false;
            return _repository.SaveAsync(BuildDto(), ct);
        }

        // ----- ISalesStatsRecorder -----

        public void RecordSold(string bookId) => RecordSold(bookId, default);

        public void RecordSold(string bookId, in SaleContext ctx)
        {
            if (string.IsNullOrEmpty(bookId)) return;
            if (!_loaded)
                Debug.LogWarning($"{LogPrefix} RecordSold before AfterLoadAsync; mutation will still apply.");

            if (!TryResolveGenre(bookId, out var genre))
                return;

            var key = genre.ToConfigValue();
            var newCount = (_soldByGenre.TryGetValue(key, out var current) ? current : 0) + 1;
            _soldByGenre[key] = newCount;
            _total++;

            // Optional per-location attribution (skipped when no location context).
            if (!string.IsNullOrEmpty(ctx.LocationId))
                Bump(GetOrAddInner(_soldByLocationGenre, ctx.LocationId), key);

            // Optional per-day attribution (skipped when no day context).
            if (ctx.Day > 0)
                Bump(GetOrAddInner(_soldByDayGenre, ctx.Day), key);

            // In-memory only; the real write is deferred to the next save cycle (BeforeSaveAsync).
            _dirty = true;
            _save.MarkDirty();

            Changed?.Invoke(new SalesStatsChange(genre, newCount, _total, bookId));
        }

        private static Dictionary<string, int> GetOrAddInner<TKey>(
            Dictionary<TKey, Dictionary<string, int>> outer, TKey key)
        {
            if (!outer.TryGetValue(key, out var inner))
            {
                inner = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                outer[key] = inner;
            }
            return inner;
        }

        private static void Bump(Dictionary<string, int> counts, string key)
            => counts[key] = (counts.TryGetValue(key, out var c) ? c : 0) + 1;

        // ----- internals -----

        private bool TryResolveGenre(string bookId, out BookGenre genre)
        {
            genre = default;

            var book = _configs.Get<BookConfig>(bookId);
            if (book == null)
            {
                Debug.LogWarning($"{LogPrefix} sold book '{bookId}' has no BookConfig; not counted.");
                return false;
            }

            if (!BookGenreExtensions.TryParseGenre(book.Genre, out genre))
            {
                Debug.LogWarning($"{LogPrefix} book '{bookId}' has unknown genre '{book.Genre}'; not counted.");
                return false;
            }

            return true;
        }

        private SalesStatsStateDto BuildDto()
        {
            var byLocation = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
            foreach (var location in _soldByLocationGenre)
                byLocation[location.Key] =
                    new Dictionary<string, int>(location.Value, StringComparer.OrdinalIgnoreCase);

            var byDay = new Dictionary<int, Dictionary<string, int>>();
            foreach (var day in _soldByDayGenre)
                byDay[day.Key] =
                    new Dictionary<string, int>(day.Value, StringComparer.OrdinalIgnoreCase);

            return new SalesStatsStateDto
            {
                SoldByGenre = new Dictionary<string, int>(_soldByGenre, StringComparer.OrdinalIgnoreCase),
                SoldByLocationGenre = byLocation,
                SoldByDayGenre = byDay
            };
        }

        /// <summary>
        /// Reads the owning service's live counters minus a frozen baseline snapshot — i.e. "sold since the
        /// baseline was captured". Nested so it can read the live per-day dict for scoped single-day max.
        /// </summary>
        private sealed class ScopedSalesStatsReader : ISalesStatsReader
        {
            private readonly SalesStatsService _live;
            private readonly SalesStatsStateDto _baseline;

            public ScopedSalesStatsReader(SalesStatsService live, SalesStatsStateDto baseline)
            {
                _live = live;
                _baseline = baseline ?? new SalesStatsStateDto();
            }

            public int TotalSold
            {
                get
                {
                    var baseTotal = 0;
                    if (_baseline.SoldByGenre != null)
                        foreach (var v in _baseline.SoldByGenre.Values) baseTotal += v;
                    return Math.Max(0, _live.TotalSold - baseTotal);
                }
            }

            public int GetSold(BookGenre genre)
                => Math.Max(0, _live.GetSold(genre) - BaseGenre(_baseline.SoldByGenre, genre));

            public int GetSold(BookGenre genre, string locationId)
            {
                var baseVal = 0;
                if (!string.IsNullOrEmpty(locationId) && _baseline.SoldByLocationGenre != null
                    && _baseline.SoldByLocationGenre.TryGetValue(locationId, out var byGenre))
                    baseVal = BaseGenre(byGenre, genre);
                return Math.Max(0, _live.GetSold(genre, locationId) - baseVal);
            }

            public int GetSoldOnDay(int day)
            {
                if (!_live._soldByDayGenre.TryGetValue(day, out var liveByGenre)) return 0;

                Dictionary<string, int> baseByGenre = null;
                _baseline.SoldByDayGenre?.TryGetValue(day, out baseByGenre);

                var sum = 0;
                foreach (var kv in liveByGenre)
                {
                    var baseVal = baseByGenre != null && baseByGenre.TryGetValue(kv.Key, out var b) ? b : 0;
                    var scoped = kv.Value - baseVal;
                    if (scoped > 0) sum += scoped;
                }
                return sum;
            }

            public int GetSoldOnDay(int day, BookGenre genre)
                => Math.Max(0, _live.GetSoldOnDay(day, genre) - BaseDayGenre(day, genre));

            public int GetMaxSoldInSingleDay(BookGenre genre)
            {
                var key = genre.ToConfigValue();
                var max = 0;
                foreach (var pair in _live._soldByDayGenre)
                {
                    var liveVal = pair.Value.TryGetValue(key, out var c) ? c : 0;
                    var scoped = liveVal - BaseDayGenre(pair.Key, genre);
                    if (scoped > max) max = scoped;
                }
                return max;
            }

            private static int BaseGenre(Dictionary<string, int> dict, BookGenre genre)
                => dict != null && dict.TryGetValue(genre.ToConfigValue(), out var v) ? v : 0;

            private int BaseDayGenre(int day, BookGenre genre)
                => _baseline.SoldByDayGenre != null
                   && _baseline.SoldByDayGenre.TryGetValue(day, out var byGenre)
                    ? BaseGenre(byGenre, genre)
                    : 0;
        }
    }
}
