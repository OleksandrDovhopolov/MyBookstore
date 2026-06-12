namespace Analytics
{
    public interface IAnalyticsConsentService
    {
        bool CanSendAnalytics { get; }

        bool CanSendAttributionData { get; }

        bool CanSendPersonalizedAdsData { get; }

        void SetAnalyticsConsent(bool isAllowed);

        void SetAttributionConsent(bool isAllowed);

        void SetPersonalizedAdsConsent(bool isAllowed);
    }
}
