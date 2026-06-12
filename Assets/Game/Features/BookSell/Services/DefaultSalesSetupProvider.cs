using System;
using System.Collections.Generic;
using System.Linq;
using Book.Sell.Domain;
using Game.Configs;
using Game.Configs.Models;
using UnityEngine;

namespace Book.Sell.Services
{
    /// <summary>
    /// Fallback provider used until the Preparation phase exists. First location from
    /// LocationConfig + first <see cref="MaxShelfBooks"/> books from BookConfig. No decor.
    /// </summary>
    public sealed class DefaultSalesSetupProvider : ISalesSetupProvider
    {
        private const string LogPrefix = "[Sales.Setup]";
        public const int MaxShelfBooks = 8;

        private readonly IConfigsService _configs;

        public DefaultSalesSetupProvider(IConfigsService configs)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        public SalesSessionSetup BuildForDay(int day)
        {
            var locations = _configs.GetAll<LocationConfig>();
            if (locations.Count == 0)
            {
                Debug.LogWarning($"{LogPrefix} LocationConfig is empty. Returning an empty setup.");
                return new SalesSessionSetup(day, locationId: null, shelfBookIds: Array.Empty<string>());
            }

            var location = locations[0];

            var books = _configs.GetAll<BookConfig>();
            if (books.Count == 0)
            {
                Debug.LogWarning($"{LogPrefix} BookConfig is empty. Shelf will be empty.");
                return new SalesSessionSetup(day, location.Id, Array.Empty<string>());
            }

            var shelfIds = books.Take(MaxShelfBooks).Select(b => b.Id).ToList();
            Debug.Log($"{LogPrefix} Day {day}: location={location.Id}, shelf size={shelfIds.Count}.");
            return new SalesSessionSetup(day, location.Id, shelfIds);
        }
    }
}
