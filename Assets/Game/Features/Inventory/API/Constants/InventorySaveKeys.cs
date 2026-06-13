namespace Game.Inventory.API
{
    /// <summary>
    /// Save module keys owned by the Inventory feature.
    /// </summary>
    public static class InventorySaveKeys
    {
        /// <summary>Module holding the player's inventory state (uniques + stacks).</summary>
        public const string State = "inventory";
        public const int StateSchemaVersion = 1;
    }
}
