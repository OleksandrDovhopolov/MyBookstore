using System.Collections.Generic;

namespace Analytics
{
    public interface IAnalyticsMappingConfig
    {
        bool TryGetEventMapping(
            string providerId,
            string eventName,
            out string mappedEventName,
            out IReadOnlyDictionary<string, string> parameterMappings);
    }
}
