using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Inventory.API
{
    /// <summary>
    /// Player inventory. Sync read accessors hit an in-memory cache populated on save load
    /// (<see cref="Save.ISaveHook"/>); async writers persist through <see cref="IInventoryRepository"/>.
    /// The split lets synchronous consumers (e.g. Preparation provider) coexist with a future
    /// async/server-backed repository.
    /// </summary>
    public interface IInventoryService
    {
        // ----- sync read -----

        IReadOnlyList<InventoryItem> GetAll();
        IReadOnlyList<InventoryItem> GetByCategory(string categoryId);
        bool Has(string itemId);

        /// <summary>1 for unique, N for stack, 0 if absent.</summary>
        int GetCount(string itemId);

        // ----- async write -----

        /// <summary>
        /// Adds <paramref name="amount"/> copies of <paramref name="itemId"/> into <paramref name="categoryId"/>.
        /// Unique categories ignore amount &gt; 1 and are idempotent on duplicate adds.
        /// Unknown category id → logs an error and returns without mutating state.
        /// </summary>
        UniTask AddAsync(string itemId, string categoryId, int amount, CancellationToken ct);

        /// <summary>Batch variant used by FTUE and similar bulk seeders. Persists once at the end.</summary>
        UniTask AddBatchAsync(IEnumerable<InventoryItem> items, CancellationToken ct);

        /// <summary>
        /// Removes up to <paramref name="amount"/> copies. Returns false if the inventory does not
        /// hold that many (no partial removal — state stays as it was).
        /// </summary>
        UniTask<bool> RemoveAsync(string itemId, int amount, CancellationToken ct);

        event Action<InventoryChangeEvent> Changed;
    }
}
