namespace Game.SalesStats.API
{
    /// <summary>
    /// Save module keys owned by the SalesStats feature.
    /// </summary>
    public static class SalesStatsSaveKeys
    {
        public const string State = "sales_stats";

        // v2 adds SoldByLocationGenre + SoldByDayGenre. v1 saves load cleanly (missing maps => empty).
        public const int StateSchemaVersion = 2;
    }
}
