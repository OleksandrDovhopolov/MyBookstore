namespace Book.Sell.API
{
    /// <summary>
    /// Per-component breakdown of a recommendation's score. Sum equals <see cref="Total"/>.
    /// Kept separate so the UI can show "matched: genre + 2 qualities" and the tests can assert.
    /// </summary>
    public readonly struct ScoreBreakdown
    {
        public int GenrePoints { get; }
        public int QualityPoints { get; }
        public int PricePoints { get; }
        public int LocationPoints { get; }

        public int Total => GenrePoints + QualityPoints + PricePoints + LocationPoints;

        public ScoreBreakdown(int genre, int quality, int price, int location)
        {
            GenrePoints = genre;
            QualityPoints = quality;
            PricePoints = price;
            LocationPoints = location;
        }
    }
}
