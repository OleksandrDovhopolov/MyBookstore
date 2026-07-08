using Game.Configs.Models;

namespace Game.Configs
{
    /// <summary>
    /// Shared resolution of <see cref="DayConfig"/> by 1-based day index: exact match, else fall back to the
    /// last configured day so indices past the authored content don't drop into an empty setup. Factored out
    /// of <c>MorningContextResolver.FindByDayIndex</c> so the sales setup providers (GAME-6 §Этап 5) reuse the
    /// exact same "exact-match, fallback-last" rule instead of re-implementing it by eye.
    /// </summary>
    public static class DayConfigLookup
    {
        public static DayConfig ByIndex(IConfigsService configs, int dayIndex)
        {
            if (configs == null) return null;

            DayConfig match = null;
            DayConfig lastByIndex = null;

            foreach (var day in configs.GetAll<DayConfig>())
            {
                if (day == null) continue;

                if (day.DayIndex == dayIndex)
                {
                    match = day;
                    break;
                }

                if (lastByIndex == null || day.DayIndex > lastByIndex.DayIndex)
                    lastByIndex = day;
            }

            return match ?? lastByIndex;
        }
    }
}
