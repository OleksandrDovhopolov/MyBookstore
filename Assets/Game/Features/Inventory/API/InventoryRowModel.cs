namespace Game.Inventory.API
{
    public readonly struct InventoryRowModel
    {
        public InventoryRowModel(
            string spriteId,
            int count,
            string itemId,
            InventoryRowStyle style,
            bool isHighlighted)
        {
            SpriteId = spriteId;
            Count = count;
            ItemId = itemId;
            Style = style;
            IsHighlighted = isHighlighted;
        }

        public string SpriteId { get; }
        public int Count { get; }
        public string ItemId { get; }
        public InventoryRowStyle Style { get; }
        public bool IsHighlighted { get; }
    }
}
