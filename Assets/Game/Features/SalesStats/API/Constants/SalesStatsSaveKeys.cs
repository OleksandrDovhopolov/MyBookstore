namespace Game.SalesStats.API
{
    /// <summary>
    /// Save module keys owned by the SalesStats feature.
    /// </summary>
    public static class SalesStatsSaveKeys
    {
        public const string State = "sales_stats";

        // Release baseline: all pre-release saves are wiped, so the public schema starts at v1.
        public const int StateSchemaVersion = 1;
    }
}
