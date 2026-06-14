using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Inventory.API
{
    /// <summary>
    /// Plugin for "use this item" actions. Implementations are registered in DI and discovered by
    /// <see cref="IInventoryUseRouter"/> via <see cref="SupportedCategoryId"/>.
    /// </summary>
    public interface IInventoryItemUseHandler
    {
        string SupportedCategoryId { get; }

        UniTask<InventoryUseResult> UseAsync(InventoryItem item, CancellationToken ct);
    }
}
