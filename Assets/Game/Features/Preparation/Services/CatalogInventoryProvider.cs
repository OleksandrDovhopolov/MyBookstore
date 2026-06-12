using System;
using System.Collections.Generic;
using Game.Configs;
using Game.Configs.Models;

namespace Game.Preparation.Services
{
    /// <summary>
    /// Временная реализация инвентаря: отдаёт весь каталог BookConfig как «owned».
    /// Заменяется на провайдер поверх <c>DayProgressState.OwnedBookIds</c> в задаче A.
    /// </summary>
    public sealed class CatalogInventoryProvider : IPreparationInventoryProvider
    {
        private readonly IConfigsService _configs;

        public CatalogInventoryProvider(IConfigsService configs)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        public IReadOnlyList<BookConfig> GetOwnedBooks() => _configs.GetAll<BookConfig>();
    }
}
