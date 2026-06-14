using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Inventory.API
{
    /// <summary>
    /// Dispatches "use item" requests from UI to the matching <see cref="IInventoryItemUseHandler"/>
    /// and applies the resulting <see cref="InventoryUseResult.ConsumeAfterUse"/> on the inventory.
    /// </summary>
    public interface IInventoryUseRouter
    {
        UniTask<InventoryUseResult> UseAsync(string itemId, CancellationToken ct);
    }
}
