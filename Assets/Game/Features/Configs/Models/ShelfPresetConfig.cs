namespace Game.Configs.Models
{
    /// <summary>
    /// Curated shelf composition for active-request cheat/testing flows.
    /// File: shelf_presets.json (JSON array).
    /// </summary>
    [ConfigFile("shelf_presets")]
    public sealed class ShelfPresetConfig : IConfig
    {
        public string Id { get; set; }
        public string DisplayNameKey { get; set; }
        public string[] BookIds { get; set; }
    }
}
