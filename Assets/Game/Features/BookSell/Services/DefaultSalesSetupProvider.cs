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
    /// Fallback-provider до появления фазы Подготовки. Первый location из LocationConfig +
    /// первые <see cref="MaxShelfBooks"/> книг из BookConfig. Без декора.
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
                Debug.LogWarning($"{LogPrefix} В LocationConfig нет ни одной локации. Setup пустой.");
                return new SalesSessionSetup(day, locationId: null, shelfBookIds: Array.Empty<string>());
            }

            var location = locations[0];

            var books = _configs.GetAll<BookConfig>();
            if (books.Count == 0)
            {
                Debug.LogWarning($"{LogPrefix} В BookConfig нет ни одной книги. Полка пустая.");
                return new SalesSessionSetup(day, location.Id, Array.Empty<string>());
            }

            var shelfIds = books.Take(MaxShelfBooks).Select(b => b.Id).ToList();
            Debug.Log($"{LogPrefix} День {day}: локация={location.Id}, книг на полке={shelfIds.Count}.");
            return new SalesSessionSetup(day, location.Id, shelfIds);
        }
    }
}
