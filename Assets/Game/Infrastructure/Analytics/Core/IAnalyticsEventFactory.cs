using System.Collections.Generic;

namespace Analytics
{
    public interface IAnalyticsEventFactory
    {
        IAnalyticsEvent CreateEvent(
            string eventName,
            IReadOnlyDictionary<string, object> parameters = null);
    }
}
