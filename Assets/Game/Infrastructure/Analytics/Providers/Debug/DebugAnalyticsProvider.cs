using System.Text;
using UnityEngine;

namespace Analytics
{
    public sealed class DebugAnalyticsProvider : AnalyticsProviderBase
    {
        private readonly IAnalyticsConfig _config;

        public DebugAnalyticsProvider(IAnalyticsConfig config)
            : base(config)
        {
            _config = config;
        }

        public override string ProviderId => AnalyticsProviderIds.Debug;

        public override bool IsEnabled => base.IsEnabled && _config.IsDebugLoggingEnabled;

        public override void TrackEvent(IAnalyticsEvent analyticsEvent)
        {
            if (!IsEnabled || analyticsEvent == null)
            {
                return;
            }

            var builder = new StringBuilder();
            builder.Append("[Analytics][Debug] ");
            builder.Append(analyticsEvent.Name);

            foreach (var parameter in analyticsEvent.Parameters)
            {
                builder.Append(" | ");
                builder.Append(parameter.Key);
                builder.Append('=');
                builder.Append(parameter.Value);
            }

            Debug.LogWarning(builder.ToString());
        }
    }
}
