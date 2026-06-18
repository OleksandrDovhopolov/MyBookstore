using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Inventory.API;
using UnityEngine;

namespace Game.Inventory.Services
{
    /// <inheritdoc cref="IInventoryUseRouter"/>
    public sealed class InventoryUseRouter : IInventoryUseRouter
    {
        private const string LogPrefix = "[Inventory.Use]";

        private readonly IInventoryService _inventory;
        private readonly Dictionary<string, IInventoryItemUseHandler> _handlersByCategory;

        public InventoryUseRouter(IInventoryService inventory, IReadOnlyList<IInventoryItemUseHandler> handlers)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _handlersByCategory = new Dictionary<string, IInventoryItemUseHandler>(StringComparer.Ordinal);
            if (handlers != null)
            {
                for (var i = 0; i < handlers.Count; i++)
                {
                    var h = handlers[i];
                    if (h == null || string.IsNullOrEmpty(h.SupportedCategoryId)) continue;
                    _handlersByCategory[h.SupportedCategoryId] = h;
                }
            }
        }

        public async UniTask<InventoryUseResult> UseAsync(string itemId, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(itemId))
                return InventoryUseResult.Fail("empty item id");

            var all = _inventory.GetAll();
            InventoryItem owned = null;
            for (var i = 0; i < all.Count; i++)
            {
                if (string.Equals(all[i].ItemId, itemId, StringComparison.Ordinal)) { owned = all[i]; break; }
            }
            if (owned == null)
                return InventoryUseResult.Fail("not owned");

            if (!_handlersByCategory.TryGetValue(owned.CategoryId, out var handler))
                return InventoryUseResult.Fail($"no handler for category '{owned.CategoryId}'");

            // TODO: clicking an inventory item should only show info, not invoke handlers.
            // Re-enable when "Use from inventory" becomes a real action again.
            // var result = await handler.UseAsync(owned, ct);
            // if (result.Success && result.ConsumeAfterUse)
            // {
            //     var removed = await _inventory.RemoveAsync(itemId, 1, ct);
            //     if (!removed)
            //         Debug.LogWarning($"{LogPrefix} use ok but RemoveAsync failed for '{itemId}'.");
            // }
            // return result;

            await UniTask.Yield(ct);
            return InventoryUseResult.Ok(consume: false, message: "use disabled — info-only mode");
        }
    }
}
