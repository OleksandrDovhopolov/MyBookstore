using Game.DayCycle.Morning.Model;

namespace Game.DayCycle.Morning
{
    /// <summary>
    /// Превращает номер дня в утренний контекст: ищет DayConfig по DayIndex,
    /// иначе отдаёт детерминированный fallback. Чистая логика без Save/Unity —
    /// удобно покрывается EditMode-тестами.
    /// </summary>
    public interface IMorningContextResolver
    {
        MorningDayContext Resolve(int dayIndex);
    }
}
