using System.Collections.Generic;

namespace Analytics
{
    public sealed class DefaultAnalyticsEventMapper : IAnalyticsEventMapper
    {
        private readonly IAnalyticsMappingConfig _mappingConfig;

        public DefaultAnalyticsEventMapper(IAnalyticsMappingConfig mappingConfig)
        {
            _mappingConfig = mappingConfig;
        }

        public IAnalyticsEvent Map(
            IAnalyticsEvent sourceEvent,
            string targetProviderId)
        {
            if (sourceEvent == null)
            {
                return null;
            }

            if (_mappingConfig == null ||
                !_mappingConfig.TryGetEventMapping(targetProviderId, sourceEvent.Name, out var eventName, out var parameterMappings))
            {
                return sourceEvent;
            }

            var mappedParameters = new Dictionary<string, object>();
            foreach (var parameter in sourceEvent.Parameters)
            {
                if (parameterMappings != null &&
                    parameterMappings.TryGetValue(parameter.Key, out var mappedParameter))
                {
                    if (string.IsNullOrWhiteSpace(mappedParameter))
                    {
                        continue;
                    }

                    mappedParameters[mappedParameter] = parameter.Value;
                    continue;
                }

                mappedParameters[parameter.Key] = parameter.Value;
            }

            return new AnalyticsEvent(
                string.IsNullOrWhiteSpace(eventName) ? sourceEvent.Name : eventName,
                mappedParameters);
        }
    }
}
