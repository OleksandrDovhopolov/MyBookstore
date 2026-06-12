namespace Game.Preparation.Domain
{
    /// <summary>
    /// Лимиты выбора книг на день. В MVP значения задаются константами в PreparationSessionService;
    /// миграция в EconomyConfig — отдельная задача.
    /// </summary>
    public sealed class PreparationCapacity
    {
        public int MinDailyBooks { get; }
        public int DailyBookSlots { get; }

        public PreparationCapacity(int minDailyBooks, int dailyBookSlots)
        {
            MinDailyBooks = minDailyBooks;
            DailyBookSlots = dailyBookSlots;
        }
    }
}
