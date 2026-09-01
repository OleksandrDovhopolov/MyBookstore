using UnityEngine;

namespace Analytics
{
    /// <summary>
    /// Single source of truth for the "development vs release build" split used by the analytics
    /// configs. Kept in one place so <see cref="AnalyticsConfigSO"/> and
    /// <see cref="DefaultAnalyticsConfig"/> cannot drift apart.
    /// </summary>
    public static class AnalyticsBuildContext
    {
        /// <summary>
        /// True in the Editor and in a player built with "Development Build" ticked; false in a
        /// regular release build. Debug logging and the reported environment derive from this, so a
        /// release build is silent and reports "production" without anyone flipping a flag by hand.
        /// </summary>
        public static bool IsDevelopmentBuild => Debug.isDebugBuild;

        /// <summary>Environment reported by every event when this is not a development build.</summary>
        public const string ProductionEnvironment = "production";

        /// <summary>Fallback environment for development builds with no explicit value configured.</summary>
        public const string DevelopmentEnvironment = "development";
    }
}
