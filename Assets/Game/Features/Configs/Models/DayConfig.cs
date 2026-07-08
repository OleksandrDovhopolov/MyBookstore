namespace Game.Configs.Models
{
    [ConfigFile("days")]
    public sealed class DayConfig : IConfig
    {
        public string Id { get; set; }
        public int DayIndex { get; set; }
        public string Title { get; set; }
        public string WeatherId { get; set; }
        public string EventId { get; set; }

        /// <summary>Диалоги квест-персонажей, запланированные на этот день (GAME-6 §Этап 5). Спавнер-декоратор
        /// (<c>QuestSchedulingCustomerSpawner</c>) добавляет по квест-персонажу на каждый id — он приходит
        /// первым и заводит беседу.</summary>
        public string[] ScheduledDialogueIds { get; set; }
    }
}
