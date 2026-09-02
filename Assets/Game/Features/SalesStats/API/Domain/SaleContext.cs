namespace Game.SalesStats.API
{
    /// <summary>
    /// Context captured at the moment a book is committed as sold, so the recorder can attribute the
    /// sale to a location and a game day in addition to its genre. Optional dimensions degrade
    /// gracefully: an empty <see cref="LocationId"/> skips per-location tracking and a non-positive
    /// <see cref="Day"/> skips per-day tracking. When <see cref="SoldGenre"/> is empty, the recorder
    /// falls back to the sold book's primary genre.
    /// </summary>
    public readonly struct SaleContext
    {
        public readonly string LocationId;
        public readonly int Day;
        public readonly string SoldGenre;

        public SaleContext(string locationId, int day, string soldGenre = null)
        {
            LocationId = locationId;
            Day = day;
            SoldGenre = soldGenre;
        }
    }
}
