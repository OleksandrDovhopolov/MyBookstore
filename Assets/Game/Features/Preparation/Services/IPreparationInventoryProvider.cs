using System.Collections.Generic;
using Game.Configs.Models;

namespace Game.Preparation.Services
{
    /// <summary>
    /// Source of "books available to the player" for the Preparation UI.
    /// <see cref="DayProgressInventoryProvider"/> reads <c>DayProgressState.OwnedBookIds</c>;
    /// the starter set is written there by <c>FtueBootstrapper</c> on first launch.
    /// </summary>
    public interface IPreparationInventoryProvider
    {
        IReadOnlyList<BookConfig> GetOwnedBooks();
    }
}
