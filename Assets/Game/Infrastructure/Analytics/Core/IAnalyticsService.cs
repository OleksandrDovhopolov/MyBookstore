namespace Analytics
{
    public interface IAnalyticsService
    {
        bool IsInitialized { get; }

        void Initialize();

        void TrackEvent(IAnalyticsEvent analyticsEvent);

        void SetUserId(string userId);

        void SetUserProperty(string key, string value);

        void Flush();
    }
}
