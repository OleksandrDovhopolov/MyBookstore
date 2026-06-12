namespace Analytics
{
    public interface IAnalyticsRouter
    {
        bool ShouldSendToProvider(
            IAnalyticsEvent analyticsEvent,
            IAnalyticsProvider provider);
    }
}
