namespace Book.Sell.API
{
    /// <summary>
    /// Per-component breakdown of a recommendation's score. Sum equals <see cref="Total"/>.
    /// Kept separate so the UI can show "matched: genre + 2 tags" and the tests can assert.
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
