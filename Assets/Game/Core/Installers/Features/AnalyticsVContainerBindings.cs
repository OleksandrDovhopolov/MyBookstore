using System.Collections.Generic;
using System.Linq;
using Analytics;
using UnityEngine;
using VContainer;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope — analytics must be available globally)
    // Phase 0/1: NullAnalyticsService logs events through Debug.Log so we can verify event flow in
    // the Editor without a real provider. Future: replace registration with a composite provider
    // (Firebase, GameAnalytics, AppsFlyer) — IAnalyticsService consumers don't change.
    public static class AnalyticsVContainerBindings
    {
        public static void RegisterAnalytics(this IContainerBuilder builder)
        {
            builder.Register<IAnalyticsService, NullAnalyticsService>(Lifetime.Singleton);
        }

        private sealed class NullAnalyticsService : IAnalyticsService
        {
            private const string LogPrefix = "[Analytics]";

            public bool IsInitialized => true;

            public void Initialize() { }

            public void TrackEvent(IAnalyticsEvent analyticsEvent)
            {
                if (analyticsEvent == null) return;
                var payload = analyticsEvent.Parameters == null || analyticsEvent.Parameters.Count == 0
                    ? string.Empty
                    : " " + string.Join(", ", analyticsEvent.Parameters.Select(Format));
                Debug.Log($"{LogPrefix} {analyticsEvent.Name}{payload}");
            }

            public void SetUserId(string userId) { }
            public void SetUserProperty(string key, string value) { }
            public void Flush() { }

            private static string Format(KeyValuePair<string, object> kv) =>
                kv.Value == null ? $"{kv.Key}=null" : $"{kv.Key}={kv.Value}";
        }
    }
}
