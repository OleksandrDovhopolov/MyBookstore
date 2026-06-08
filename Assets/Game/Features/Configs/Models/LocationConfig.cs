namespace Game.Configs.Models
{
    /// <summary>
    /// Конфиг локации. Поля иллюстративны — финальный набор выравнивается
    /// с Notion-задачей «Data-driven конфиги». Файл: locations.json (JSON-массив).
    /// </summary>
    [ConfigFile("locations")]
    public sealed class LocationConfig : IConfig
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public int UnlockCost { get; set; }
        public int RequiredLevel { get; set; }
    }
}
