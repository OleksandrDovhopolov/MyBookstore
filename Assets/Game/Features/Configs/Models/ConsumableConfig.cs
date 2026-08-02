namespace Game.Configs.Models
{
    /// <summary>Stackable consumable inventory item. File: consumables.json.</summary>
    [ConfigFile("consumables")]
    public sealed class ConsumableConfig : IConfig
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string DescriptionKey { get; set; }
    }
}
