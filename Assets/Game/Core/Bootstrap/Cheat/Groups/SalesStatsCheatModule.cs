using System;
using System.Collections.Generic;
using System.Threading;
using cheatModule;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.SalesStats.API;
using Save;
using UnityEngine;

namespace Game.Cheat
{
    /// <summary>
    /// Cheat group that bumps the persistent per-genre counters (save module "sales_stats").
    /// Per genre that has at least one book in configs: sold buttons (+1 / +10) and excellent-pick
    /// buttons (+1 / +5). Both recorders resolve the genre from the book config themselves, so the
    /// cheat only needs a representative book id; a save is forced so the increment lands on disk now.
    /// Bulk amounts match the quest content: Eddi's tasks need 10-15 sold, Milly's needs 5 picks.
    /// </summary>
    public class SalesStatsCheatModule : ICheatsModule
    {
        private const string SoldGroup = "Sales Stats";
        private const string PicksGroup = "Active Picks";
        private const string LogTag = "[SalesStatsCheat]";

        private const int BulkSoldAmount = 10;
        private const int BulkPickAmount = 5;

        private readonly ISalesStatsRecorder _recorder;
        private readonly ISalesStatsReader _reader;
        private readonly IConfigsService _configs;
        private readonly ISaveService _save;
        private readonly CancellationToken _ct;

        public SalesStatsCheatModule(
            ISalesStatsRecorder recorder,
            ISalesStatsReader reader,
            IConfigsService configs,
            ISaveService save,
            CancellationToken ct)
        {
            _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
            _reader = reader;
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _ct = ct;
        }

        public void Initialize(ICheatsContainer cheatsContainer)
        {
            // Both recorders take a book id and resolve its genre, so pick one representative book per genre.
            foreach (var pair in BuildRepresentativeBookByGenre())
            {
                var genre = pair.Key;
                var bookId = pair.Value;

                cheatsContainer.AddItem<CheatButtonItem>(item =>
                    item.OnClick($"+1 {genre}", () => AddSoldAsync(genre, bookId, 1).Forget())
                        .WithGroup(SoldGroup));

                cheatsContainer.AddItem<CheatButtonItem>(item =>
                    item.OnClick($"+{BulkSoldAmount} {genre}",
                            () => AddSoldAsync(genre, bookId, BulkSoldAmount).Forget())
                        .WithGroup(SoldGroup));

                cheatsContainer.AddItem<CheatButtonItem>(item =>
                    item.OnClick($"+1 pick {genre}", () => AddPickAsync(genre, bookId, 1).Forget())
                        .WithGroup(PicksGroup));

                cheatsContainer.AddItem<CheatButtonItem>(item =>
                    item.OnClick($"+{BulkPickAmount} pick {genre}",
                            () => AddPickAsync(genre, bookId, BulkPickAmount).Forget())
                        .WithGroup(PicksGroup));
            }
        }

        private Dictionary<BookGenre, string> BuildRepresentativeBookByGenre()
        {
            var map = new Dictionary<BookGenre, string>();
            foreach (var book in _configs.GetAll<BookConfig>())
            {
                if (book == null || string.IsNullOrEmpty(book.Id)) continue;
                if (!BookGenreExtensions.TryParseGenre(book.PrimaryGenre, out var genre)) continue;
                if (!map.ContainsKey(genre)) map[genre] = book.Id;
            }
            return map;
        }

        private async UniTaskVoid AddSoldAsync(BookGenre genre, string bookId, int amount)
        {
            // No bulk recorder API: RecordSold is +1 per call (in-memory, marks save dirty).
            for (var i = 0; i < amount; i++) _recorder.RecordSold(bookId);
            await _save.SaveAsync(_ct);     // flush so it lands in the "sales_stats" module now
            var total = _reader?.GetSold(genre) ?? -1;
            Debug.Log($"{LogTag} +{amount} sold {genre} (now {total}).");
        }

        private async UniTaskVoid AddPickAsync(BookGenre genre, string bookId, int amount)
        {
            // SaleContext is ignored by RecordActivePick (the counter is genre-only), so default is fine.
            for (var i = 0; i < amount; i++) _recorder.RecordActivePick(bookId, default);
            await _save.SaveAsync(_ct);
            var total = _reader?.GetExcellentPicks(genre) ?? -1;

            // Progress is scoped to the quest task's baseline, so a bump before the quest activates
            // counts as zero — log the raw counter to make that visible while cheating.
            Debug.Log($"{LogTag} +{amount} pick {genre} (now {total}).");
        }
    }
}
