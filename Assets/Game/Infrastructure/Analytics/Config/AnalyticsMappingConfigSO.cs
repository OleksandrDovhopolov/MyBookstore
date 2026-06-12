using System;
using System.Collections.Generic;
using UnityEngine;

namespace Analytics
{
    [CreateAssetMenu(menuName = "Game/Analytics/Analytics Mapping Config", fileName = "AnalyticsMappingConfig")]
    public sealed class AnalyticsMappingConfigSO : ScriptableObject, IAnalyticsMappingConfig
    {
        [SerializeField] private List<AnalyticsProviderMapping> _providerMappings = new();

        public bool TryGetEventMapping(
            string providerId,
            string eventName,
            out string mappedEventName,
            out IReadOnlyDictionary<string, string> parameterMappings)
        {
            mappedEventName = null;
            parameterMappings = null;

            foreach (var providerMapping in _providerMappings)
            {
                if (providerMapping == null ||
                    !string.Equals(providerMapping.ProviderId, providerId, StringComparison.Ordinal))
                {
                    continue;
                }

                return providerMapping.TryGetEventMapping(eventName, out mappedEventName, out parameterMappings);
            }

            return false;
        }
    }

    [Serializable]
    public sealed class AnalyticsProviderMapping
    {
        [SerializeField] private string _providerId;
        [SerializeField] private List<AnalyticsEventMapping> _eventMappings = new();

        public string ProviderId => _providerId;

        public bool TryGetEventMapping(
            string eventName,
            out string mappedEventName,
            out IReadOnlyDictionary<string, string> parameterMappings)
        {
            mappedEventName = null;
            parameterMappings = null;

            foreach (var eventMapping in _eventMappings)
            {
                if (eventMapping == null ||
                    !string.Equals(eventMapping.SourceEventName, eventName, StringComparison.Ordinal))
                {
                    continue;
                }

                mappedEventName = eventMapping.TargetEventName;
                parameterMappings = eventMapping.ParameterMappings;
                return true;
            }

            return false;
        }
    }

    [Serializable]
    public sealed class AnalyticsEventMapping
    {
        [SerializeField] private string _sourceEventName;
        [SerializeField] private string _targetEventName;
        [SerializeField] private List<AnalyticsParameterMapping> _parameterMappings = new();

        public string SourceEventName => _sourceEventName;

        public string TargetEventName => _targetEventName;

        public IReadOnlyDictionary<string, string> ParameterMappings
        {
            get
            {
                var result = new Dictionary<string, string>();
                foreach (var mapping in _parameterMappings)
                {
                    if (mapping == null || string.IsNullOrWhiteSpace(mapping.SourceParameterName))
                    {
                        continue;
                    }

                    result[mapping.SourceParameterName] = mapping.TargetParameterName;
                }

                return result;
            }
        }
    }

    [Serializable]
    public sealed class AnalyticsParameterMapping
    {
        [SerializeField] private string _sourceParameterName;
        [SerializeField] private string _targetParameterName;

        public string SourceParameterName => _sourceParameterName;

        public string TargetParameterName => _targetParameterName;
    }
}
