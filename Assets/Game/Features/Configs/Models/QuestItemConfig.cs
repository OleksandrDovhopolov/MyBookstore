namespace Game.Configs.Models
{
    /// <summary>Non-consumable story item shown in the inventory. File: quest_items.json.</summary>
    [ConfigFile("quest_items")]
    public sealed class QuestItemConfig : IConfig
    {
        public string Id { get; set; }
        public string DisplayNameKey { get; set; }
        public string DescriptionKey { get; set; }
    }
}
