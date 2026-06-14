using System;
using System.Collections.Generic;

namespace Analytics
{
    public sealed class AnalyticsEvent : IAnalyticsEvent
    {
        public AnalyticsEvent(string name, IReadOnlyDictionary<string, object> parameters = null)
        {
            Name = name;
            Parameters = parameters != null
                ? new Dictionary<string, object>(parameters)
                : new Dictionary<string, object>();
        }

        public string Name { get; }

        public IReadOnlyDictionary<string, object> Parameters { get; }

        public override string ToString()
        {
            return $"{Name} ({Parameters.Count} params)";
        }
    }
}
