using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Inventory.API
{
    /// <summary>
    /// Persistence seam under <see cref="IInventoryService"/>. The MVP implementation talks to
    /// <c>ISaveService</c>; a future HTTP-backed implementation can be swapped in without changes
    /// to the service or its consumers.
    ///
    /// The repository is intentionally typed against <see cref="InventoryStateDto"/> rather than
    /// the service's internal cache to keep the seam free of implementation classes.
    /// </summary>
    public interface IInventoryRepository
    {
        UniTask<InventoryStateDto> LoadAsync(CancellationToken ct);
        UniTask SaveAsync(InventoryStateDto state, CancellationToken ct);
    }
}
