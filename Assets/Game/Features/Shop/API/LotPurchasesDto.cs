namespace Game.Shop.API
{
    /// <summary>
    /// Persisted per-lot purchase counter. Standalone class (not inlined into the parent dict value)
    /// so that refresh-window fields can be added without replacing the parent schema.
    /// </summary>
    public sealed class LotPurchasesDto
    {
        public int Purchases { get; set; }
        public int LastPurchasedDay { get; set; }
        public int PurchasesToday { get; set; }
    }
}
