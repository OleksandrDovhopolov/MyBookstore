namespace Game.Shop.API
{
    /// <summary>
    /// What a player pays to buy a <see cref="ShopLot"/>. Phase 0 uses <c>"gold"</c> exclusively.
    /// Phase 3 may introduce <c>"gems"</c> or <c>"inapp:&lt;product_id&gt;"</c> as currency strings.
    /// Free lots (<see cref="Amount"/> == 0) skip the charge step but still go through the pipeline.
    /// </summary>
    public readonly struct ShopPrice
    {
        public string Currency { get; }
        public int Amount { get; }

        public ShopPrice(string currency, int amount)
        {
            Currency = currency;
            Amount = amount;
        }
    }
}
