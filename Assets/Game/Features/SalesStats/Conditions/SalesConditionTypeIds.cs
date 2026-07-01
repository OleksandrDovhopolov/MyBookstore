using System;
using System.Collections.Generic;

namespace Game.SalesStats.Conditions
{
    /// <summary>
    /// Single source of truth for the sales condition <c>type</c> ids — used by quests to detect which
    /// task conditions need a baseline (scoped reader). Avoids scattering literal strings.
    /// </summary>
    public static class SalesConditionTypeIds
    {
        public const string SoldGenre = SoldGenreConditionFactory.TypeId;
        public const string SoldGenreAtLocation = SoldGenreAtLocationConditionFactory.TypeId;
        public const string SoldGenreInSingleDay = SoldGenreInSingleDayConditionFactory.TypeId;

        private static readonly HashSet<string> Set =
            new(StringComparer.OrdinalIgnoreCase) { SoldGenre, SoldGenreAtLocation, SoldGenreInSingleDay };

        public static bool Contains(string type) => !string.IsNullOrEmpty(type) && Set.Contains(type);
    }
}
