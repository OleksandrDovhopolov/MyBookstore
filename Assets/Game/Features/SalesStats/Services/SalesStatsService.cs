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
    public sealed class SalesStatsService : ISalesStatsService, ISaveHook
    {
        private const string LogPrefix = "[SalesStats]";

        private readonly ISaveService _save;
        private readonly ISalesStatsRepository _repository;
        private readonly IConfigsService _configs;

        // Keyed by genre config value (BookGenre.ToConfigValue()); case-insensitive for safety.
        private readonly Dictionary<string, int> _soldByGenre = new(StringComparer.OrdinalIgnoreCase);
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

        // ----- ISaveHook -----

        public async UniTask AfterLoadAsync(CancellationToken ct)
        {
            var dto = await _repository.LoadAsync(ct);

            _soldByGenre.Clear();
            _total = 0;

            // Normalize fills every known genre with 0 and drops unknown keys (BookGenreCounts).
            foreach (var pair in BookGenreCounts.Normalize(dto?.SoldByGenre))
            {
                _soldByGenre[pair.Key] = pair.Value;
                _total += pair.Value;
            }

            _loaded = true;
            _dirty = false;
            Debug.Log($"{LogPrefix} loaded: total={_total}.");
        }

        public UniTask BeforeSaveAsync(CancellationToken ct)
        {
            // Batched flush: only writes when something actually changed since the last persist.
            if (!_dirty) return UniTask.CompletedTask;
            _dirty = false;
            return _repository.SaveAsync(BuildDto(), ct);
        }

        // ----- ISalesStatsRecorder -----

        public void RecordSold(string bookId)
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

            // In-memory only; the real write is deferred to the next save cycle (BeforeSaveAsync).
            _dirty = true;
            _save.MarkDirty();

            Changed?.Invoke(new SalesStatsChange(genre, newCount, _total, bookId));
        }

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
            => new SalesStatsStateDto
            {
                SoldByGenre = new Dictionary<string, int>(_soldByGenre, StringComparer.OrdinalIgnoreCase)
            };
    }
}
