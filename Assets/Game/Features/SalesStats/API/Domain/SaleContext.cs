namespace Game.SalesStats.API
{
    /// <summary>
    /// Context captured at the moment a book is committed as sold, so the recorder can attribute the
    /// sale to a location and a game day in addition to its genre. Optional dimensions degrade
    /// gracefully: an empty <see cref="LocationId"/> skips per-location tracking and a non-positive
    /// <see cref="Day"/> skips per-day tracking, leaving the lifetime per-genre tally unaffected.
    /// </summary>
    public readonly struct SaleContext
    {
        public readonly string LocationId;
        public readonly int Day;

        public SaleContext(string locationId, int day)
        {
            LocationId = locationId;
            Day = day;
        }
    }
}
