using System;
using System.Collections.Generic;
using Game.Decor;
using VContainer.Unity;

namespace Game.Bootstrap.Analytics
{
    public sealed class DecorAnalyticsListener : IStartable, IDisposable
    {
        private readonly IDecorPlacementService _decor;
        private readonly global::Analytics.IAnalyticsService _analytics;

        public DecorAnalyticsListener(IDecorPlacementService decor, global::Analytics.IAnalyticsService analytics)
        {
            _decor = decor ?? throw new ArgumentNullException(nameof(decor));
            _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
        }

        public void Start()
        {
            _decor.PlacementActionPerformed += OnPlacementActionPerformed;
        }

        public void Dispose()
        {
            _decor.PlacementActionPerformed -= OnPlacementActionPerformed;
        }

        private void OnPlacementActionPerformed(DecorPlacementChange change)
        {
            var parameters = new Dictionary<string, object>();
            AnalyticsParameterBag.AddString(parameters, global::Analytics.AnalyticsParameterNames.DecorId, change.DecorId);
            AnalyticsParameterBag.AddString(parameters, global::Analytics.AnalyticsParameterNames.SlotId, change.SlotId);
            AnalyticsParameterBag.AddString(parameters, global::Analytics.AnalyticsParameterNames.Action, change.Action);

            _analytics.TrackEvent(new global::Analytics.AnalyticsEvent(
                global::Analytics.AnalyticsEventNames.DecorChanged,
                parameters));
        }
    }
}
