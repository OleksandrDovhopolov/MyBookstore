using System.Collections.Generic;
using Analytics;

namespace AnalyticsTests.Editor
{
    public sealed class TestAnalyticsConfig : IAnalyticsConfig
    {
        public bool IsAnalyticsEnabled { get; set; } = true;

        public bool IsDebugLoggingEnabled { get; set; }

        public string Environment { get; set; } = "test";

        public IReadOnlyCollection<string> EnabledProviderIds => EnabledProviderIdsValue;

        public IReadOnlyCollection<string> EnabledProviderIdsValue { get; set; } = new[]
        {
            AnalyticsProviderIds.Debug,
            AnalyticsProviderIds.Firebase
        };

        public int MaxQueueSize => MaxQueueSizeValue;

        public int MaxQueueSizeValue { get; set; } = 100;

        public int MaxEventNameLength => MaxEventNameLengthValue;

        public int MaxEventNameLengthValue { get; set; } = 40;

        public int MaxParameterKeyLength => MaxParameterKeyLengthValue;

        public int MaxParameterKeyLengthValue { get; set; } = 40;

        public int MaxParameterCount => MaxParameterCountValue;

        public int MaxParameterCountValue { get; set; } = 50;

        public bool SendEventsWithoutUserId { get; set; } = true;
    }
}
