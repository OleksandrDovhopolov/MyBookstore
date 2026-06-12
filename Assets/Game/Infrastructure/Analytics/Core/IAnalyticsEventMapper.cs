namespace Analytics
{
    public interface IAnalyticsEventMapper
    {
        IAnalyticsEvent Map(
            IAnalyticsEvent sourceEvent,
            string targetProviderId);
    }
}
