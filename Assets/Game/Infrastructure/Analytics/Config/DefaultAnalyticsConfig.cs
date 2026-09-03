using System.Collections.Generic;

namespace Analytics
{
    public sealed class DefaultAnalyticsConfig : IAnalyticsConfig
    {
        private static readonly string[] DefaultEnabledProviderIds =
        {
            AnalyticsProviderIds.Debug,
            AnalyticsProviderIds.Firebase
        };

        public bool IsAnalyticsEnabled => true;

        // Mirrors AnalyticsConfigSO on purpose: this is the fallback used when the SO is not assigned
        // on BootstrapInstaller, so a forgotten assignment must not resurrect debug logging in a
        // release build.
        public bool IsDebugLoggingEnabled => AnalyticsBuildContext.IsDevelopmentBuild;

        public string Environment => AnalyticsBuildContext.IsDevelopmentBuild
            ? AnalyticsBuildContext.DevelopmentEnvironment
            : AnalyticsBuildContext.ProductionEnvironment;

        public IReadOnlyCollection<string> EnabledProviderIds => DefaultEnabledProviderIds;

        public int MaxQueueSize => 100;

        public int MaxEventNameLength => 40;

        public int MaxParameterKeyLength => 40;

        public int MaxParameterCount => 25;

        public bool SendEventsWithoutUserId => true;
    }
}
