using System.Collections.Generic;
using Game.Configs.Models;

namespace Game.Preparation.Services
{
    /// <summary>
    /// Источник «доступных игроку книг» для UI Подготовки. В MVP реализация
    /// (<see cref="CatalogInventoryProvider"/>) отдаёт весь каталог BookConfig;
    /// после задачи FTUE-пресет (A) сюда подключится реальный owned-инвентарь.
    /// </summary>
    public interface IPreparationInventoryProvider
    {
        IReadOnlyList<BookConfig> GetOwnedBooks();
    }
}
