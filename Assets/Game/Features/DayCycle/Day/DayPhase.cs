namespace Game.DayCycle.Day
{
    /// <summary>
    /// Фаза игрового дня core loop: Утро → Подготовка → Продажа → Итоги.
    /// Хранится в общем <see cref="DayProgressState"/> и определяет, какой экран показать.
    /// </summary>
    public enum DayPhase
    {
        Morning = 0,
        Preparation = 1,
        Sales = 2,
        Results = 3
    }
}
