namespace Game.Configs.Models
{
    /// <summary>
    /// Конфиг игрового события. Поля иллюстративны — финальный набор
    /// выравнивается с Notion-задачей. Файл: events.json (JSON-массив).
    /// </summary>
    [ConfigFile("events")]
    public sealed class EventConfig : IConfig
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string StartUtc { get; set; }
        public string EndUtc { get; set; }
        public float RewardMultiplier { get; set; }
    }
}
