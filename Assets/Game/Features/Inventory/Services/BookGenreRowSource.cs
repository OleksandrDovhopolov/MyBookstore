using System;
using System.Collections.Generic;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using UnityEngine;

namespace Game.Inventory.Services
{
    public sealed class BookGenreRowSource : IInventoryRowSource
    {
        private const string LogPrefix = "[InventoryRows]";

        private readonly IInventoryService _inventory;
        private readonly IConfigsService _configs;

        public BookGenreRowSource(IInventoryService inventory, IConfigsService configs)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        public int Order => 0;

        public IEnumerable<InventoryRowModel> BuildRows()
        {
            var counts = BuildCountsByGenre();
            foreach (BookGenre genre in Enum.GetValues(typeof(BookGenre)))
            {
                counts.TryGetValue(genre, out var count);
                yield return new InventoryRowModel(
                    genre.ToConfigValue(),
                    count,
                    genre.ToConfigValue(),
                    InventoryRowStyle.Default,
                    false);
            }
        }

        private Dictionary<BookGenre, int> BuildCountsByGenre()
        {
            var counts = new Dictionary<BookGenre, int>();
            foreach (BookGenre genre in Enum.GetValues(typeof(BookGenre)))
                counts[genre] = 0;

            var items = _inventory.GetByCategory(InventoryCategories.Book);
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (!_configs.TryGet<BookConfig>(item.ItemId, out var book) || book == null)
                {
                    Debug.LogWarning($"{LogPrefix} Missing BookConfig for category '{InventoryCategories.Book}' item '{item.ItemId}'.");
                    continue;
                }

                if (!BookGenreExtensions.TryParseGenre(book.PrimaryGenre, out var genre)) continue;
                counts[genre] += item.Count;
            }

            return counts;
        }
    }
}
