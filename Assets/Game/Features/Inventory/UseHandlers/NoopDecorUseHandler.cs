using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Inventory.API;
using UnityEngine;

namespace Game.Inventory.UseHandlers
{
    /// <summary>
    /// Stub: real decor activation (adding to <c>SalesSessionSetup.DecorIds</c> for the day)
    /// lands with the decor feature. For now we just log and keep the item in inventory.
    /// </summary>
    public sealed class NoopDecorUseHandler : IInventoryItemUseHandler
    {
        public string SupportedCategoryId => InventoryCategories.Decor;

        public UniTask<InventoryUseResult> UseAsync(InventoryItem item, CancellationToken ct)
        {
            Debug.Log($"[Decor] activate stub: {item.ItemId}");
            return UniTask.FromResult(InventoryUseResult.Ok(consume: false, message: "decor activation stub"));
        }
    }
}
