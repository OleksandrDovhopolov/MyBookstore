namespace Analytics
{
    public sealed class StubAnalyticsConsentService : IAnalyticsConsentService
    {
        public bool CanSendAnalytics => true;

        public bool CanSendAttributionData => true;

        public bool CanSendPersonalizedAdsData => true;

        public void SetAnalyticsConsent(bool isAllowed)
        {
        }

        public void SetAttributionConsent(bool isAllowed)
        {
        }

        public void SetPersonalizedAdsConsent(bool isAllowed)
        {
        }
    }
}
