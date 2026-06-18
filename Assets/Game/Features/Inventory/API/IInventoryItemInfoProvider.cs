namespace Game.Inventory.API
{
    /// <summary>
    /// Plug-in for the inventory UI: returns a short status string for an item in the supported
    /// category. Optional — categories without a registered provider show no info next to the item id.
    /// Resolved as a collection (<c>IReadOnlyList&lt;IInventoryItemInfoProvider&gt;</c>) so multiple
    /// features can each contribute their own per-category provider.
    /// </summary>
    public interface IInventoryItemInfoProvider
    {
        string SupportedCategoryId { get; }
        string GetInfoFor(string itemId);
    }
}
