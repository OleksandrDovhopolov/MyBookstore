namespace Game.Inventory.API
{
    /// <summary>
    /// How a category stores its items.
    ///   Unique: at most one entry per item id; Count is always 1.
    ///   Stack:  any number per id, aggregated by Count.
    /// </summary>
    public enum ItemStackingMode
    {
        Unique = 0,
        Stack = 1
    }
}
