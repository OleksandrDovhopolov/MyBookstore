namespace Game.Inventory.API
{
    /// <summary>
    /// Stable string ids for built-in inventory categories. New categories can be added by
    /// registering them in <see cref="IItemCategoryRegistry"/> at startup.
    /// </summary>
    public static class InventoryCategories
    {
        public const string Book = "book";
        public const string Decor = "decor";
        public const string PuzzlePiece = "puzzle_piece";
    }
}
