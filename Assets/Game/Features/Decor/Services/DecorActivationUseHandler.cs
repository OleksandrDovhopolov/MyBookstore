using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using UnityEngine;

namespace Game.Decor.Services
{
    /// <summary>
    /// Inventory use handler for category "decor". When the player Use'es a decor:
    /// try to autoplace it into the first free compatible slot. If no compatible empty slot,
    /// the call becomes a no-op + log (the player is expected to open DecorPlacementWindow
    /// to manually swap something out).
    /// </summary>
    public sealed class DecorActivationUseHandler : IInventoryItemUseHandler
    {
        private const string LogTag = "[DecorActivate]";

        private readonly IDecorPlacementService _placement;
        private readonly IConfigsService _configs;

        public DecorActivationUseHandler(IDecorPlacementService placement, IConfigsService configs)
        {
            _placement = placement;
            _configs = configs;
        }

        public string SupportedCategoryId => InventoryCategories.Decor;

        public async UniTask<InventoryUseResult> UseAsync(InventoryItem item, CancellationToken ct)
        {
            // Decor is unique — if it's already placed in any slot, do nothing instead of duplicating.
            var existingSlot = FindPlacedSlot(item.ItemId);
            if (!string.IsNullOrEmpty(existingSlot))
            {
                Debug.Log($"{LogTag} '{item.ItemId}' already placed in '{existingSlot}' — nothing to do.");
                return InventoryUseResult.Ok(consume: false, message: $"already placed in {existingSlot}");
            }

            var decorConfig = _configs.Get<DecorConfig>(item.ItemId);
            if (decorConfig == null)
            {
                Debug.LogWarning($"{LogTag} DecorConfig '{item.ItemId}' missing — cannot autoplace.");
                return InventoryUseResult.Fail("decor config missing");
            }

            var shop = _configs.Get<BookShopConfig>(DecorPlacementService.HardcodedBookShopId);
            if (shop?.DecorSlots == null || shop.DecorSlots.Length == 0)
            {
                return InventoryUseResult.Fail("no slots on current bookshop");
            }

            for (var i = 0; i < shop.DecorSlots.Length; i++)
            {
                var slot = shop.DecorSlots[i];
                if (slot == null) continue;
                if (decorConfig.PositionType != slot.PositionType) continue;
                if ((int)decorConfig.Size > (int)slot.MaxSize) continue;
                if (!string.IsNullOrEmpty(_placement.GetDecorInSlot(slot.Id))) continue;

                var result = await _placement.PlaceAsync(item.ItemId, slot.Id, ct);
                if (result == DecorPlacementResult.Success)
                {
                    Debug.Log($"{LogTag} Autoplaced '{item.ItemId}' into '{slot.Id}'.");
                    return InventoryUseResult.Ok(consume: false, message: $"placed in {slot.Id}");
                }
            }

            Debug.Log($"{LogTag} No free compatible slot for '{item.ItemId}'. Open placement screen to swap.");
            return InventoryUseResult.Ok(consume: false, message: "no free compatible slot");
        }

        private string FindPlacedSlot(string decorId)
        {
            foreach (var entry in _placement.GetAllPlacements())
            {
                if (string.Equals(entry.DecorId, decorId, System.StringComparison.OrdinalIgnoreCase))
                    return entry.SlotId;
            }
            return null;
        }
    }
}
