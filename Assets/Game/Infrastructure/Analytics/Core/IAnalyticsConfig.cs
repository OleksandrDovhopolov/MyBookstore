using System.Collections.Generic;

namespace Analytics
{
    public interface IAnalyticsConfig
    {
        bool IsAnalyticsEnabled { get; }

        bool IsDebugLoggingEnabled { get; }

        string Environment { get; }

        IReadOnlyCollection<string> EnabledProviderIds { get; }

        int MaxQueueSize { get; }

        int MaxEventNameLength { get; }

        int MaxParameterKeyLength { get; }

        int MaxParameterCount { get; }

        bool SendEventsWithoutUserId { get; }
    }
}
