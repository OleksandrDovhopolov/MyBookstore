using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Book.Sell.Domain;
using Book.Sell.Services;
using Game.Configs;
using Game.Configs.Models;
using Game.Preparation.Domain;
using Save;
using UnityEngine;

namespace Game.Preparation.Services
{
    /// <summary>
    /// Поставщик дневной настройки Sales поверх preparation.session.
    /// Заменяет <c>DefaultSalesSetupProvider</c>: если игрок прошёл Подготовку — берём его выбор,
    /// иначе fallback на первый location + первые N книг (как раньше) с warn-логом.
    ///
    /// TODO: ISalesSetupProvider синхронный, а ISaveService.GetModuleAsync async. После SaveService.LoadAsync
    /// модули лежат в памяти, поэтому .GetAwaiter().GetResult() безопасен. Если интерфейс станет async — убрать хак.
    /// </summary>
    public sealed class PreparationSalesSetupProvider : ISalesSetupProvider
    {
        private const string LogPrefix = "[Sales.Setup]";
        private const int FallbackMaxShelfBooks = 8;

        private readonly ISaveService _save;
        private readonly IConfigsService _configs;

        public PreparationSalesSetupProvider(ISaveService save, IConfigsService configs)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        public SalesSessionSetup BuildForDay(int day)
        {
            var state = _save
                .GetModuleAsync<PreparationSessionState>(PreparationSaveKeys.Session, CancellationToken.None)
                .GetAwaiter().GetResult();

            if (state == null || state.Day != day || state.SelectedBookIds == null || state.SelectedBookIds.Count == 0)
            {
                Debug.LogWarning($"{LogPrefix} нет preparation.session для day={day} — fallback на каталог.");
                return BuildFallback(day);
            }

            var shelf = state.SelectedBookIds.ToArray();
            var decor = state.SelectedDecorIds?.ToArray() ?? Array.Empty<string>();
            Debug.Log($"{LogPrefix} day={day} location={state.LocationId} shelf={shelf.Length} (preparation).");
            return new SalesSessionSetup(day, state.LocationId, shelf, decor);
        }

        private SalesSessionSetup BuildFallback(int day)
        {
            var locations = _configs.GetAll<LocationConfig>();
            if (locations.Count == 0)
            {
                Debug.LogWarning($"{LogPrefix} LocationConfig is empty — empty setup.");
                return new SalesSessionSetup(day, locationId: null, shelfBookIds: Array.Empty<string>());
            }

            var location = locations[0];
            var books = _configs.GetAll<BookConfig>();
            if (books.Count == 0)
            {
                Debug.LogWarning($"{LogPrefix} BookConfig is empty — empty shelf.");
                return new SalesSessionSetup(day, location.Id, Array.Empty<string>());
            }

            var shelf = new List<string>(FallbackMaxShelfBooks);
            for (int i = 0; i < books.Count && shelf.Count < FallbackMaxShelfBooks; i++)
                shelf.Add(books[i].Id);

            Debug.Log($"{LogPrefix} day={day} location={location.Id} shelf={shelf.Count} (fallback).");
            return new SalesSessionSetup(day, location.Id, shelf);
        }
    }
}
