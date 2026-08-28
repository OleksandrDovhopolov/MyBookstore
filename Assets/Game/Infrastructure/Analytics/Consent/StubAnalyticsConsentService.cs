namespace Analytics
{
    /// <summary>
    /// TEST DOUBLE ONLY. Grants every consent category unconditionally and ignores every setter.
    /// Never register this in production wiring — use <see cref="ConsentService"/>, which persists a real
    /// decision and defaults to denying everything.
    /// </summary>
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
