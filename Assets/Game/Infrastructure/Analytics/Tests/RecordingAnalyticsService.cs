using System.Collections.Generic;

namespace Analytics
{
    public sealed class RecordingAnalyticsService : IAnalyticsService
    {
        public List<IAnalyticsEvent> Events { get; } = new();

        public string UserId { get; private set; }
        public bool IsInitialized { get; private set; } = true;

        public void Initialize()
        {
            IsInitialized = true;
        }

        public void TrackEvent(IAnalyticsEvent analyticsEvent)
        {
            Events.Add(analyticsEvent);
        }

        public void SetUserId(string userId)
        {
            UserId = userId;
        }

        public void SetUserProperty(string key, string value)
        {
        }

        public void Flush()
        {
        }
    }
}
