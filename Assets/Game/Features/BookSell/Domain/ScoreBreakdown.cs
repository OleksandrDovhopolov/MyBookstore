namespace Book.Sell.Domain
{
    /// <summary>
    /// Покомпонентный разбор очков рекомендации. Сумма = <see cref="Total"/>.
    /// Сохраняем отдельно для UI («совпало: жанр + 2 тега») и тестов.
    /// </summary>
    public readonly struct ScoreBreakdown
    {
        public int GenrePoints { get; }
        public int TagPoints { get; }
        public int MoodPoints { get; }
        public int PricePoints { get; }
        public int LocationPoints { get; }

        public int Total => GenrePoints + TagPoints + MoodPoints + PricePoints + LocationPoints;

        public ScoreBreakdown(int genre, int tag, int mood, int price, int location)
        {
            GenrePoints = genre;
            TagPoints = tag;
            MoodPoints = mood;
            PricePoints = price;
            LocationPoints = location;
        }
    }
}
