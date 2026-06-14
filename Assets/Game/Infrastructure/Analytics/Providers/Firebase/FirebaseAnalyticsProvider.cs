using System;
using System.Collections.Generic;
using Firebase.Analytics;
using UnityEngine;

namespace Analytics
{
    public sealed class FirebaseAnalyticsProvider : AnalyticsProviderBase
    {
        public FirebaseAnalyticsProvider(IAnalyticsConfig config)
            : base(config)
        {
        }

        public override string ProviderId => AnalyticsProviderIds.Firebase;

        public override void TrackEvent(IAnalyticsEvent analyticsEvent)
        {
            if (!IsEnabled || analyticsEvent == null)
            {
                return;
            }

            FirebaseAnalytics.LogEvent(analyticsEvent.Name, CreateFirebaseParameters(analyticsEvent.Parameters));
        }

        public override void SetUserId(string userId)
        {
            if (IsEnabled && !string.IsNullOrWhiteSpace(userId))
            {
                FirebaseAnalytics.SetUserId(userId);
            }
        }

        public override void SetUserProperty(string key, string value)
        {
            if (IsEnabled && !string.IsNullOrWhiteSpace(key))
            {
                FirebaseAnalytics.SetUserProperty(key, value);
            }
        }

        private static Parameter[] CreateFirebaseParameters(IReadOnlyDictionary<string, object> parameters)
        {
            if (parameters == null || parameters.Count == 0)
            {
                return Array.Empty<Parameter>();
            }

            var result = new List<Parameter>(parameters.Count);
            foreach (var parameter in parameters)
            {
                switch (parameter.Value)
                {
                    case string stringValue:
                        result.Add(new Parameter(parameter.Key, stringValue));
                        break;
                    case int intValue:
                        result.Add(new Parameter(parameter.Key, intValue));
                        break;
                    case long longValue:
                        result.Add(new Parameter(parameter.Key, longValue));
                        break;
                    case float floatValue:
                        result.Add(new Parameter(parameter.Key, (double)floatValue));
                        break;
                    case double doubleValue:
                        result.Add(new Parameter(parameter.Key, doubleValue));
                        break;
                    case bool boolValue:
                        result.Add(new Parameter(parameter.Key, boolValue ? 1L : 0L));
                        break;
                    default:
                        Debug.LogWarning($"[Analytics][Firebase] Unsupported parameter skipped: {parameter.Key}");
                        break;
                }
            }

            return result.ToArray();
        }
    }
}
