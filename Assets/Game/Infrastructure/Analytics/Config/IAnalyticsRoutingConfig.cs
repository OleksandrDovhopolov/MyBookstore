namespace Analytics
{
    public interface IAnalyticsRoutingConfig
    {
        bool ShouldSendToProvider(string eventName, string providerId);
    }
}
