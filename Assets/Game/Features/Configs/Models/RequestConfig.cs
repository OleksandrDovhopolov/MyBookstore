namespace Game.Configs.Models
{
    /// <summary>
    /// Конфиг запроса покупателя (sales-shape). Заменил прежнюю quest-структуру
    /// (bookId/rewardSoft/timeLimitSeconds) — она была не про продажи, а про задание.
    /// Файл: requests.json (JSON-массив).
    /// Спека: docs/INPROGRESS/Продажа.md, план: docs/INPROGRESS/SalesFeatureImplementationPlan.md.
    /// </summary>
    [ConfigFile("requests")]
    public sealed class RequestConfig : IConfig
    {
        public string Id { get; set; }

        /// <summary>Реплика клиента — игрок читает её и пытается «разгадать», что подойдёт.</summary>
        public string Text { get; set; }

        /// <summary>Желаемый жанр (точное совпадение — +3 к score). Может быть массивом для синонимов/альтернатив.</summary>
        public string[] DesiredGenres { get; set; }

        /// <summary>Желаемые темы/теги (+2 каждый матч).</summary>
        public string[] DesiredTags { get; set; }

        /// <summary>Желаемый тон/настроение (+1 каждый матч).</summary>
        public string[] DesiredMood { get; set; }

        /// <summary>Максимальная цена книги в бюджете клиента. 0 — без ограничения (+1 за матч пропускается).</summary>
        public int MaxPrice { get; set; }

        /// <summary>Сложность 1..5 — для дизайнерского балансирования сложности дня. Scoring не использует.</summary>
        public int Difficulty { get; set; }

        /// <summary>Бонус-золото поверх BasePrice книги при tier=Excellent.</summary>
        public int BaseRewardGold { get; set; }
    }
}
