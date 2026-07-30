namespace Book.Sell.API
{
    /// <summary>
    /// Outcome category for a recommendation. Scoring is all-or-nothing: a book either satisfies every
    /// condition of the request (<see cref="Excellent"/>) or it does not (<see cref="Failed"/>).
    /// <see cref="Skipped"/> is the non-scoring tier — the player honestly declined to recommend
    /// anything. NOT the same as <see cref="Failed"/>.
    /// <para>
    /// Value 1 is intentionally skipped: it belonged to a removed "Normal" tier and the enum is
    /// serialized into the "book_sell.last_day_result" save module, so the remaining values must not shift.
    /// </para>
    /// </summary>
    public enum RecommendationTier
    {
        Failed = 0,
        Excellent = 2,
        Skipped = 3
    }
}
