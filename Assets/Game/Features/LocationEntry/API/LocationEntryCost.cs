namespace Game.LocationEntry.API
{
    /// <summary>
    /// Resolved per-visit entry fee for a location: which currency, the final amount to charge
    /// (<see cref="Total"/>, clamped ≥ 0), and its breakdown (<see cref="Base"/> from the location config
    /// plus the signed <see cref="DecorDelta"/> contributed by active decor). The fee is a sunk visit cost
    /// charged on entering the location — see docs/SAVE_DAY_FLOW.md.
    /// </summary>
    public readonly struct LocationEntryCost
    {
        public string CurrencyId { get; }
        public int Total { get; }
        public int Base { get; }
        public int DecorDelta { get; }

        public LocationEntryCost(string currencyId, int total, int @base, int decorDelta)
        {
            CurrencyId = currencyId;
            Total = total;
            Base = @base;
            DecorDelta = decorDelta;
        }
    }
}
