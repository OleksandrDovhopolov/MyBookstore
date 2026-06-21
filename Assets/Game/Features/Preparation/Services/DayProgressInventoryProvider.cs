using System;
using System.Collections.Generic;
using Book.Sell.Services;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using UnityEngine;

namespace Game.Preparation.Services
{
    /// <summary>
    /// Player book inventory adapter for Preparation: returns BookConfigs for ids stored under
    /// category <see cref="InventoryCategories.Book"/> in the inventory.
    /// Fallback: if the inventory has no books (FTUE skipped, dev scenario, manual wipe), returns
    /// the full catalog with a warning — so Preparation doesn't stall on an empty list.
    /// </summary>
    public sealed class DayProgressInventoryProvider : IPreparationInventoryProvider
    {
        private const string LogPrefix = "[Preparation.Inventory]";

        private readonly IInventoryService _inventory;
        private readonly IConfigsService _configs;
        private readonly ISalesShelfStateService _shelfState;

        public DayProgressInventoryProvider(IInventoryService inventory, IConfigsService configs, ISalesShelfStateService shelfState)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _shelfState = shelfState ?? throw new ArgumentNullException(nameof(shelfState));
        }

        public IReadOnlyList<BookConfig> GetOwnedBooks()
        {
            var owned = _inventory.GetByCategory(InventoryCategories.Book);
            if (owned == null || owned.Count == 0)
            {
                Debug.LogWarning($"{LogPrefix} inventory book category is empty — falling back to the full catalog.");
                return FilterSold(_configs.GetAll<BookConfig>());
            }

            var result = new List<BookConfig>(owned.Count);
            for (var i = 0; i < owned.Count; i++)
            {
                if (_shelfState.IsSold(owned[i].ItemId)) continue;

                if (_configs.TryGet<BookConfig>(owned[i].ItemId, out var book))
                    result.Add(book);
                else
                    Debug.LogWarning($"{LogPrefix} BookConfig '{owned[i].ItemId}' not found in catalog — skipping.");
            }
            return result;
        }

        private IReadOnlyList<BookConfig> FilterSold(IReadOnlyList<BookConfig> books)
        {
            if (books == null || books.Count == 0) return books ?? System.Array.Empty<BookConfig>();

            var result = new List<BookConfig>(books.Count);
            for (var i = 0; i < books.Count; i++)
            {
                var book = books[i];
                if (book == null || _shelfState.IsSold(book.Id)) continue;
                result.Add(book);
            }
            return result;
        }
    }
}
