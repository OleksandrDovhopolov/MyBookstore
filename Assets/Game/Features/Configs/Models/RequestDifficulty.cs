namespace Game.Configs.Models
{
    /// <summary>
    /// Designer-facing difficulty for a sales request, 1..5 "stars".
    /// Not consumed by scoring — used for content balancing and UI display only.
    /// JSON values map directly to integers (Newtonsoft handles the conversion).
    /// </summary>
    public enum RequestDifficulty
    {
        /// <summary>Sentinel for entries that omit the difficulty field in JSON.</summary>
        Unknown = 0,

        Trivial = 1,
        Easy = 2,
        Medium = 3,
        Hard = 4,
        Expert = 5
    }
}
