using System;
using Game.Inventory.API;

namespace Game.Decor.Services
{
    /// <summary>
    /// Inventory info provider for category "decor": returns the slot id where the decor is currently
    /// placed, or "not placed" if it is unplaced. Used by InventoryWindowView to show the placement
    /// relation next to each decor row.
    /// </summary>
    public sealed class DecorPlacementInfoProvider : IInventoryItemInfoProvider
    {
        private readonly IDecorPlacementService _placement;

        public DecorPlacementInfoProvider(IDecorPlacementService placement)
        {
            _placement = placement ?? throw new ArgumentNullException(nameof(placement));
        }

        public string SupportedCategoryId => InventoryCategories.Decor;

        public string GetInfoFor(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;

            foreach (var entry in _placement.GetAllPlacements())
            {
                if (string.Equals(entry.DecorId, itemId, StringComparison.OrdinalIgnoreCase))
                    return $"placement {entry.SlotId}";
            }
            return "not placed";
        }
    }
}
