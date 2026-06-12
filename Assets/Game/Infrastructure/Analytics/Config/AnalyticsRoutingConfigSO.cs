using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Analytics
{
    [CreateAssetMenu(menuName = "Game/Analytics/Analytics Routing Config", fileName = "AnalyticsRoutingConfig")]
    public sealed class AnalyticsRoutingConfigSO : ScriptableObject, IAnalyticsRoutingConfig
    {
        [SerializeField] private List<AnalyticsRoutingRule> _rules = new();

        public bool ShouldSendToProvider(string eventName, string providerId)
        {
            if (string.IsNullOrWhiteSpace(eventName) || string.IsNullOrWhiteSpace(providerId))
            {
                return false;
            }

            foreach (var rule in _rules)
            {
                if (rule == null || !string.Equals(rule.EventName, eventName, StringComparison.Ordinal))
                {
                    continue;
                }

                return rule.ProviderIds != null && rule.ProviderIds.Contains(providerId);
            }

            return true;
        }
    }

    [Serializable]
    public sealed class AnalyticsRoutingRule
    {
        [SerializeField] private string _eventName;
        [SerializeField] private List<string> _providerIds = new();

        public string EventName => _eventName;

        public IReadOnlyCollection<string> ProviderIds => _providerIds;
    }
}
