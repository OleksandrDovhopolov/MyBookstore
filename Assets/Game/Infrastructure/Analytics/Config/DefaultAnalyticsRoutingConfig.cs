namespace Analytics
{
    public sealed class DefaultAnalyticsRoutingConfig : IAnalyticsRoutingConfig
    {
        public bool ShouldSendToProvider(string eventName, string providerId)
        {
            return true;
        }
    }
}
