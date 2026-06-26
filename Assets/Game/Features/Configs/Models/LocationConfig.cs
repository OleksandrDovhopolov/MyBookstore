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

        /// <summary>Цена открытия локации (валюта gold), списывается при покупке после выполнения условий.</summary>
        public int UnlockCost { get; set; }

        /// <summary>
        /// Плата за визит (валюта gold), списывается при каждом входе в локацию как sunk-ставка
        /// (см. docs/SAVE_DAY_FLOW.md). Отличается от <see cref="UnlockCost"/> (разовая разблокировка).
        /// Может сдвигаться активным декором (<c>DecorConfig.VisitCostDelta</c>).
        /// </summary>
        public int EntryCost { get; set; }

        /// <summary>
        /// Legacy-ярлык порога уровня. Источник истины об условиях — <see cref="Unlock"/>: если он
        /// задан, <see cref="RequiredLevel"/> игнорируется (одна истина). Самостоятельная поддержка
        /// ждёт появления провайдера уровня игрока.
        /// </summary>
        public int RequiredLevel { get; set; }

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
