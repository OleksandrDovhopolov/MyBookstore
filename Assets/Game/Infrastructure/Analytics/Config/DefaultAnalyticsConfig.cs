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

        public bool IsDebugLoggingEnabled => true;

        public string Environment => "development";

        public IReadOnlyCollection<string> EnabledProviderIds => DefaultEnabledProviderIds;

        public int MaxQueueSize => 100;

        public int MaxEventNameLength => 40;

        public int MaxParameterKeyLength => 40;

        public int MaxParameterCount => 50;

        public bool SendEventsWithoutUserId => true;
    }
}
