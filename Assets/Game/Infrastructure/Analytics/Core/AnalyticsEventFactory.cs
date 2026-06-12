using System.Collections.Generic;

namespace Analytics
{
    public sealed class AnalyticsEventFactory : IAnalyticsEventFactory
    {
        public IAnalyticsEvent CreateEvent(
            string eventName,
            IReadOnlyDictionary<string, object> parameters = null)
        {
            return new AnalyticsEvent(eventName, parameters);
        }
    }
}
