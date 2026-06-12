namespace Analytics
{
    public interface IAnalyticsQueue
    {
        int Count { get; }

        void Enqueue(IAnalyticsEvent analyticsEvent);

        bool TryDequeue(out IAnalyticsEvent analyticsEvent);

        void Clear();
    }
}
