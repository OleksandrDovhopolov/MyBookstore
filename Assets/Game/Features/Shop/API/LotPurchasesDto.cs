namespace Game.Shop.API
{
    /// <summary>
    /// Persisted per-lot purchase counter. Standalone class (not inlined into the parent dict value)
    /// so that future fields (last-purchased timestamp, refresh window start) can be added without a
    /// schema migration.
    /// </summary>
    public sealed class LotPurchasesDto
    {
        public int Purchases { get; set; }
    }
}
