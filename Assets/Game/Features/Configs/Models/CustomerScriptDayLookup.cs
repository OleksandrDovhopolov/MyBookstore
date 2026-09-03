using System;
using System.Collections.Generic;

namespace Game.Configs.Models
{
    /// <summary>
    /// Shared day-based lookup rules for authored customer scripts.
    /// </summary>
    public static class CustomerScriptDayLookup
    {
        public static bool MatchesDay(CustomerScriptConfig script, int day)
            => script?.DayIndex == day;

        public static IReadOnlyList<string> PassiveGenresForDay(IEnumerable<CustomerScriptConfig> scripts, int day)
        {
            if (scripts == null)
                return Array.Empty<string>();

            var genres = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var script in scripts)
            {
                if (!MatchesDay(script, day)) continue;

                var attempts = script.PassiveAttempts;
                if (attempts == null) continue;

                for (var i = 0; i < attempts.Length; i++)
                {
                    var genre = attempts[i]?.Genre?.Trim();
                    if (string.IsNullOrWhiteSpace(genre)) continue;
                    if (seen.Add(genre))
                        genres.Add(genre);
                }
            }

            return genres;
        }
    }
}
