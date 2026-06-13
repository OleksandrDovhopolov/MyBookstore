using System;

namespace Game.Inventory.API
{
    /// <summary>
    /// Declaration of an inventory category: how its items stack, plus a display name for UI.
    /// </summary>
    public sealed class ItemCategory
    {
        public string Id { get; }
        public ItemStackingMode StackingMode { get; }
        public string DisplayName { get; }

        public ItemCategory(string id, ItemStackingMode stackingMode, string displayName)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            StackingMode = stackingMode;
            DisplayName = displayName ?? id;
        }
    }
}
