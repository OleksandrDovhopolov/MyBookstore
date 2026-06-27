namespace Game.Configs.Models
{
    /// <summary>
    /// Конфиг локации — *где* лавка стоит сегодня (центр, торговый центр, парк, ...). Отвечает
    /// за спрос (demand genres/tags для пассивных продаж и scoring-бонус локации) и условия
    /// разблокировки. Слоты декора живут на <see cref="BookShopConfig"/>, не здесь — декор
    /// "едет" вместе с лавкой между локациями.
    /// Файл: locations.json (JSON-массив).
    /// </summary>
    [ConfigFile("locations")]
    public sealed class LocationConfig : IConfig
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }

        /// <summary>
        /// Плата за визит (валюта gold), списывается при каждом входе в локацию как sunk-ставка
        /// (см. docs/SAVE_DAY_FLOW.md). Отличается от разовой разблокировки (которая теперь бесплатна
        /// и автоматическая при выполнении <see cref="Unlock"/>). Может сдвигаться активным декором
        /// (<c>DecorConfig.VisitCostDelta</c>).
        /// </summary>
        public int EntryCost { get; set; }

        /// <summary>
        /// Адрес собранной сцены локации (Addressables) — задел на будущее. null = fallback на
        /// единственную <c>LocationScene</c> (<c>GameFlowSettings.LocationSceneName</c>). Пока только
        /// хранится; загрузка сцены этим полем ещё не управляется.
        /// </summary>
        public string LocationAddress { get; set; }

        /// <summary>
        /// Data-driven дерево условий разблокировки (движок Conditions): композиты all/any/not +
        /// листья с дискриминатором type (например soldGenre). Хранится сырым, парсится
        /// <c>ILocationUnlockService</c> — фича Configs не знает про доменные условия. null = нет условий.
        /// </summary>
        public Newtonsoft.Json.Linq.JObject Unlock { get; set; }

        /// <summary>Жанры повышенного спроса в этой локации (университет → science, парк → kids/romance).</summary>
        public string[] DemandGenres { get; set; }

        /// <summary>Теги/темы повышенного спроса (study, cozy, family, ...). Используются пассивными продажами и scoring-бонусом локации.</summary>
        public string[] DemandTags { get; set; }
    }
}
