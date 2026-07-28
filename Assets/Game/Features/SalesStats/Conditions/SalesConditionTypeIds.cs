using System;
using System.Collections.Generic;
using Game.SalesStats.API;
using Newtonsoft.Json.Linq;

namespace Game.SalesStats.Conditions
{
    public interface ISalesStatsBaselinePlanContributor
    {
        string Type { get; }

        void Contribute(JObject node, SalesStatsBaselineCapturePlan plan);
    }

    /// <summary>
    /// Single source of truth for the sales condition <c>type</c> ids — used by quests to detect which
    /// task conditions need a baseline (scoped reader). Avoids scattering literal strings.
    /// </summary>
    public static class SalesConditionTypeIds
    {
        public const string SoldGenre = SoldGenreConditionFactory.TypeId;
        public const string SoldGenreAtLocation = SoldGenreAtLocationConditionFactory.TypeId;
        public const string SoldGenreInSingleDay = SoldGenreInSingleDayConditionFactory.TypeId;
        public const string ActivePickGenre = ActivePickGenreConditionFactory.TypeId;

        private static readonly HashSet<string> Set =
            new(StringComparer.OrdinalIgnoreCase)
            {
                SoldGenre,
                SoldGenreAtLocation,
                SoldGenreInSingleDay,
                ActivePickGenre
            };

        public static bool Contains(string type) => !string.IsNullOrEmpty(type) && Set.Contains(type);
    }
}
