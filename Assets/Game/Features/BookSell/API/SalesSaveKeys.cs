namespace Book.Sell.API
{
    /// <summary>
    /// Public Save-module keys produced by the Sales phase, exposed in the API so downstream
    /// consumers (Results, future analytics) can read them without referencing Book.Sell internals.
    /// </summary>
    public static class SalesSaveKeys
    {
        /// <summary>Holds the latest <see cref="SalesDayResult"/>; written by the Sales controller on day completion.</summary>
        public const string LastDayResult = "book_sell.last_day_result";
        public const int LastDayResultSchemaVersion = 1;

        /// <summary>Persistent physical shelf/stock state that survives day transitions.</summary>
        public const string ShelfState = "book_sell.shelf_state";
        public const int ShelfStateSchemaVersion = 1;
    }
}
