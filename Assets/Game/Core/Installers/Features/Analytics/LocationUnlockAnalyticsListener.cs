using System;
using System.Collections.Generic;
using Game.LocationUnlock.API;
using VContainer.Unity;

namespace Game.Bootstrap.Analytics
{
    public sealed class LocationUnlockAnalyticsListener : IStartable, IDisposable
    {
        private readonly ILocationUnlockService _locations;
        private readonly global::Analytics.IAnalyticsService _analytics;

        public LocationUnlockAnalyticsListener(
            ILocationUnlockService locations,
            global::Analytics.IAnalyticsService analytics)
        {
            _locations = locations ?? throw new ArgumentNullException(nameof(locations));
            _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
        }

        public void Start()
        {
            _locations.Unlocked += OnUnlocked;
        }

        public void Dispose()
        {
            _locations.Unlocked -= OnUnlocked;
        }

        private void OnUnlocked(string locationId)
        {
            var parameters = new Dictionary<string, object>();
            AnalyticsParameterBag.AddString(parameters, global::Analytics.AnalyticsParameterNames.LocationId, locationId);

            _analytics.TrackEvent(new global::Analytics.AnalyticsEvent(
                global::Analytics.AnalyticsEventNames.LocationUnlocked,
                parameters));
        }
    }
}
