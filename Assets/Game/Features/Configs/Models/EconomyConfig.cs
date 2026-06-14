namespace Game.Configs.Models
{
    /// <summary>
    /// Global economy/balance numbers driving probabilistic passive sales (and future tuning knobs).
    /// File: economy.json — a JSON array containing a single object whose "id" must equal
    /// <see cref="SingletonId"/>. Access via <c>configs.Get&lt;EconomyConfig&gt;(EconomyConfig.SingletonId)</c>.
    /// </summary>
    [ConfigFile("economy")]
    public sealed class EconomyConfig : IConfig
    {
        public const string SingletonId = "economy";

        public string Id { get; set; }

        /// <summary>f(count) intercept: chance when count == 1 (before location/decor multipliers).</summary>
        public double BaseSaleChance { get; set; }

        /// <summary>f(count) slope: extra chance added per copy of the genre on the shelf.</summary>
        public double PerCopyChance { get; set; }

        /// <summary>f(count) ceiling: hard cap on the genre-level chance before location/decor.</summary>
        public double CapChance { get; set; }

        /// <summary>Multiplier applied when the genre is listed in LocationConfig.DemandGenres.</summary>
        public double LocationDemandMultiplier { get; set; }
    }
}
