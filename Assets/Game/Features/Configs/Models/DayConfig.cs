namespace Game.Configs.Models
{
    /// <summary>
    /// Конфиг игрового дня — источник утреннего контекста (фаза «Утро»).
    /// Резолвится по <see cref="DayIndex"/>. Файл: days.json (JSON-массив).
    /// Спека: docs/INPROGRESS/Утро.md, план: docs/INPROGRESS/MorningFeatureImplementationPlan.md.
    /// </summary>
    [ConfigFile("days")]
    public sealed class DayConfig : IConfig
    {
        public string Id { get; set; }

        /// <summary>Номер дня (1-based). По нему резолвер находит контекст для текущего дня.</summary>
        public int DayIndex { get; set; }

        public string Title { get; set; }
        public string WeatherId { get; set; }
        public string EventId { get; set; }

        /// <summary>Жанры повышенного спроса дня — читают Подготовка/Продажа.</summary>
        public string[] DemandGenres { get; set; }

        /// <summary>Теги повышенного спроса дня.</summary>
        public string[] DemandTags { get; set; }

        /// <summary>Диалоги квест-персонажей, запланированные на этот день (GAME-6 §Этап 5). Спавнер-декоратор
        /// (<c>QuestSchedulingCustomerSpawner</c>) добавляет по квест-персонажу на каждый id — он приходит
        /// первым и заводит беседу.</summary>
        public string[] ScheduledDialogueIds { get; set; }
    }
}
