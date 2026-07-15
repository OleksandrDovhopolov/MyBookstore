using System.Collections.Generic;
using Book.Sell.Domain;
using Game.Configs;
using Game.Configs.Models;
using UnityEngine;

namespace Book.Sell.Services
{
    public sealed class SalesShelfBuilder : ISalesShelfBuilder
    {
        private const string LogPrefix = "[Sales.Day]";

        private readonly IConfigsService _configs;

        public SalesShelfBuilder(IConfigsService configs)
        {
            _configs = configs;
        }

        public SalesShelf Build(IReadOnlyList<string> bookIds)
        {
            var shelf = new SalesShelf();
            if (bookIds == null) return shelf;

            for (var i = 0; i < bookIds.Count; i++)
            {
                var bookId = bookIds[i];
                var book = !string.IsNullOrEmpty(bookId)
                    ? _configs?.Get<BookConfig>(bookId)
                    : null;

                if (book == null)
                {
                    Debug.LogWarning($"{LogPrefix} BookConfig '{bookId}' not found - skipping.");
                    continue;
                }

                shelf.Add(new ShelfBook(book));
            }

            return shelf;
        }
    }
}

