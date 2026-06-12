using System;
using System.Collections.Generic;

namespace Analytics
{
    public abstract class AnalyticsProviderBase : IAnalyticsProvider
    {
        private readonly IAnalyticsConfig _config;

        protected AnalyticsProviderBase(IAnalyticsConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public abstract string ProviderId { get; }

        public virtual bool IsEnabled => IsProviderEnabled(_config.EnabledProviderIds, ProviderId);

        public bool IsInitialized { get; protected set; }

        public virtual void Initialize()
        {
            IsInitialized = true;
        }

        public abstract void TrackEvent(IAnalyticsEvent analyticsEvent);

        public virtual void SetUserId(string userId)
        {
        }

        public virtual void SetUserProperty(string key, string value)
        {
        }

        public virtual void Flush()
        {
        }

        protected static bool IsProviderEnabled(IReadOnlyCollection<string> enabledProviderIds, string providerId)
        {
            if (enabledProviderIds == null || enabledProviderIds.Count == 0)
            {
                return false;
            }

            foreach (var enabledProviderId in enabledProviderIds)
            {
                if (string.Equals(enabledProviderId, providerId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
