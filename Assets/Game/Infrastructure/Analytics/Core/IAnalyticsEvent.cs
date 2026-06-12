using System.Collections.Generic;

namespace Analytics
{
    public interface IAnalyticsEvent
    {
        string Name { get; }

        IReadOnlyDictionary<string, object> Parameters { get; }
    }
}
