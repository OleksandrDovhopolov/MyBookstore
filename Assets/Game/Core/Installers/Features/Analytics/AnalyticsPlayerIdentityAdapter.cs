using System;

namespace Game.Bootstrap.Analytics
{
    public sealed class AnalyticsPlayerIdentityAdapter : global::Analytics.IPlayerIdentityProvider
    {
        private readonly Save.Identity.IPlayerIdentityProvider _inner;

        public AnalyticsPlayerIdentityAdapter(Save.Identity.IPlayerIdentityProvider inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public string GetPlayerId() => _inner.GetPlayerId();
    }
}
