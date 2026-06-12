namespace Analytics
{
    public interface IAnalyticsEventValidator
    {
        bool Validate(IAnalyticsEvent analyticsEvent, out string error);
    }
}
