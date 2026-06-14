using System.Collections.Generic;

namespace Analytics
{
    public sealed class DefaultAnalyticsMappingConfig : IAnalyticsMappingConfig
    {
        public bool TryGetEventMapping(
            string providerId,
            string eventName,
            out string mappedEventName,
            out IReadOnlyDictionary<string, string> parameterMappings)
        {
            mappedEventName = null;
            parameterMappings = null;
            return false;
        }
    }
}
