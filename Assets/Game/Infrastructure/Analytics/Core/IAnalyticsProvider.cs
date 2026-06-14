namespace Analytics
{
    public interface IAnalyticsProvider
    {
        string ProviderId { get; }

        bool IsEnabled { get; }

        bool IsInitialized { get; }

        void Initialize();

        void TrackEvent(IAnalyticsEvent analyticsEvent);

        void SetUserId(string userId);

        void SetUserProperty(string key, string value);

        void Flush();
    }
}
