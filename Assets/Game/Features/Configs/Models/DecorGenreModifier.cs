namespace Game.Configs.Models
{
    /// <summary>
    /// Per-genre passive sale chance multiplier carried by a DecorConfig.
    /// `Multiplier &gt; 1.0` boosts the genre, `&lt; 1.0` nerfs it. Multiplier `&lt;= 0` is invalid
    /// (validator logs an error and the entry is ignored).
    /// </summary>
    public sealed class DecorGenreModifier
    {
        public string Genre { get; set; }
        public float Multiplier { get; set; }
    }
}
