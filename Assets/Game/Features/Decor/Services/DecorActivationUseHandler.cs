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
            var decorConfig = _configs.Get<DecorConfig>(item.ItemId);
            if (decorConfig == null)
            {
                Debug.LogWarning($"{LogTag} DecorConfig '{item.ItemId}' missing — cannot autoplace.");
                return InventoryUseResult.Fail("decor config missing");
            }

            var location = _configs.Get<LocationConfig>(DecorPlacementService.HardcodedLocationId);
            if (location?.DecorSlots == null || location.DecorSlots.Length == 0)
            {
                return InventoryUseResult.Fail("no slots at current location");
            }

            for (var i = 0; i < location.DecorSlots.Length; i++)
            {
                var slot = location.DecorSlots[i];
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
    }
}
