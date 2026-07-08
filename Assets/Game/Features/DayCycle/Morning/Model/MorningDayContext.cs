namespace Game.DayCycle.Morning.Model
{
    /// <summary>
    /// Разрешённый контекст утра: то, что показывается на экране и передаётся дальше по дню.
    /// Резолвится детерминированно из DayConfig по номеру дня (или fallback), поэтому
    /// перезапуск на фазе утра показывает тот же день/событие/погоду без отдельного сохранения.
    /// </summary>
    //TODO проверить нужно ли каждое поле. особенно string поля 
    public sealed class MorningDayContext
    {
        public int Day { get; set; }
        public string DayId { get; set; }
        public string Title { get; set; }
        public string WeatherId { get; set; }
        public string EventId { get; set; }

        /// <summary>true, если контекст собран из fallback (нет подходящего DayConfig).</summary>
        public bool IsFallback { get; set; }
    }
}
