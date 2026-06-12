namespace Game.DayCycle.Morning
{
    /// <summary>
    /// Безопасный контент утра, когда подходящего DayConfig нет (или конфиги не загрузились).
    /// Без модификаторов спроса — день играбелен, но «нейтрален».
    /// </summary>
    public static class MorningFallback
    {
        public const string Title = "Тихое утро";
        public const string WeatherId = "clear";
        public const string EventId = "";
        public const string SummaryText = "Спокойное утро для книжной тележки.";
        public const string HintText = "Возьмите несколько книг и откройте лавку.";
    }
}
