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
    /// Cheat group that bumps the persistent per-genre sold counters (save module "sales_stats").
    /// One "+1 {genre}" button per genre that has at least one book in configs: it records one sold
    /// book of that genre (<see cref="ISalesStatsRecorder"/> resolves the genre from the book config)
    /// and forces a save so the increment lands on disk immediately.
    /// </summary>
    public class SalesStatsCheatModule : ICheatsModule
    {
        private const string CardsGroup = "Sales Stats";
        private const string LogTag = "[SalesStatsCheat]";

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
            // RecordSold takes a book id and resolves its genre, so pick one representative book per genre.
            foreach (var pair in BuildRepresentativeBookByGenre())
            {
                var genre = pair.Key;
                var bookId = pair.Value;
                cheatsContainer.AddItem<CheatButtonItem>(item =>
                    item.OnClick($"+1 {genre}", () => AddSoldAsync(genre, bookId).Forget())
                        .WithGroup(CardsGroup));
            }
        }

        private Dictionary<BookGenre, string> BuildRepresentativeBookByGenre()
        {
            var map = new Dictionary<BookGenre, string>();
            foreach (var book in _configs.GetAll<BookConfig>())
            {
                if (book == null || string.IsNullOrEmpty(book.Id)) continue;
                if (!BookGenreExtensions.TryParseGenre(book.Genre, out var genre)) continue;
                if (!map.ContainsKey(genre)) map[genre] = book.Id;
            }
            return map;
        }

        private async UniTaskVoid AddSoldAsync(BookGenre genre, string bookId)
        {
            _recorder.RecordSold(bookId);   // in-memory +1, marks save dirty
            await _save.SaveAsync(_ct);     // flush so it lands in the "sales_stats" module now
            var total = _reader?.GetSold(genre) ?? -1;
            Debug.Log($"{LogTag} +1 {genre} (now {total}).");
        }
    }
}
