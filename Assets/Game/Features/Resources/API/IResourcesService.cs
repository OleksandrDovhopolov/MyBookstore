using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Resources.API
{
    /// <summary>
    /// Player wallet. Sync read accessors hit an in-memory cache populated on save load
    /// (<see cref="Save.ISaveHook"/>); async writers persist through <see cref="IResourcesRepository"/>.
    /// The shape mirrors <see cref="Game.Inventory.API.IInventoryService"/> — see <c>docs/INVENTORY.md</c>
    /// for the rationale behind sync read / async write.
    /// </summary>
    public interface IResourcesService
    {
        // ----- sync read -----

        IReadOnlyDictionary<string, int> GetAll();

        /// <summary>Returns 0 when the resource has never been touched.</summary>
        int GetAmount(string resourceId);

        bool Has(string resourceId, int amount);

        // ----- async write -----

        /// <summary>
        /// Adds <paramref name="amount"/> to <paramref name="resourceId"/>. Non-positive amounts
        /// are no-ops (no event, no save). <paramref name="reason"/> is audit text logged with the
        /// change event.
        /// </summary>
        UniTask AddAsync(string resourceId, int amount, string reason, CancellationToken ct);

        /// <summary>
        /// Removes <paramref name="amount"/> from <paramref name="resourceId"/>. Returns false when
        /// the balance is insufficient (state stays as it was — no partial removal). Non-positive
        /// amounts return true and do nothing.
        /// </summary>
        UniTask<bool> RemoveAsync(string resourceId, int amount, string reason, CancellationToken ct);

        event Action<ResourceChangeEvent> Changed;
    }
}
