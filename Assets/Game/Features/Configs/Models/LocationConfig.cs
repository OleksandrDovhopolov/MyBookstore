namespace Game.Configs.Models
{
    /// <summary>
    /// Конфиг локации (где лавка стоит сегодня). Расширен под пассивные продажи
    /// и scoring-бонус локации.
    /// Файл: locations.json (JSON-массив).
    /// </summary>
    [ConfigFile("locations")]
    public sealed class LocationConfig : IConfig
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public int UnlockCost { get; set; }
        public int RequiredLevel { get; set; }

        /// <summary>Жанры повышенного спроса в этой локации (университет → science, парк → kids/romance).</summary>
        public string[] DemandGenres { get; set; }

        /// <summary>Теги/темы повышенного спроса (study, cozy, family, ...). Используются пассивными продажами и scoring-бонусом локации.</summary>
        public string[] DemandTags { get; set; }
    }
}
