namespace Analytics
{
    public sealed class DefaultAnalyticsRouter : IAnalyticsRouter
    {
        private readonly IAnalyticsRoutingConfig _routingConfig;

        public DefaultAnalyticsRouter(IAnalyticsRoutingConfig routingConfig)
        {
            _routingConfig = routingConfig;
        }

        public bool ShouldSendToProvider(
            IAnalyticsEvent analyticsEvent,
            IAnalyticsProvider provider)
        {
            if (analyticsEvent == null || provider == null)
            {
                return false;
            }

            return _routingConfig == null ||
                   _routingConfig.ShouldSendToProvider(analyticsEvent.Name, provider.ProviderId);
        }
    }
}
