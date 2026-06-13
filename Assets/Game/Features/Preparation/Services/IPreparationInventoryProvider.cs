using System.Collections.Generic;
using Game.Configs.Models;

namespace Game.Preparation.Services
{
    /// <summary>
    /// Source of "books available to the player" for the Preparation UI.
    /// <see cref="DayProgressInventoryProvider"/> reads the inventory under category
    /// <c>InventoryCategories.Book</c>; the starter set is seeded there by <c>FtueBootstrapper</c>
    /// on first launch.
    /// </summary>
    public interface IPreparationInventoryProvider
    {
        IReadOnlyList<BookConfig> GetOwnedBooks();
    }
}
