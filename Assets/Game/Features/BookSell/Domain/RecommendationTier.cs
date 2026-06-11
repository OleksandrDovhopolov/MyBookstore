namespace Book.Sell.Domain
{
    /// <summary>
    /// Категория результата рекомендации. Скоринг даёт три tier'а по score
    /// (0-2 Failed / 3-5 Normal / 6+ Excellent). Skip — отдельный tier:
    /// игрок честно отказал клиенту, это НЕ Failed.
    /// </summary>
    public enum RecommendationTier
    {
        Failed = 0,
        Normal = 1,
        Excellent = 2,
        Skipped = 3
    }
}
