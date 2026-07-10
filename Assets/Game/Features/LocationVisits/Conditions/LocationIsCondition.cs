using System;
using Game.Conditions.API;
using Game.LocationVisits.API;

namespace Game.LocationVisits.Conditions
{
    /// <summary>
    /// Leaf condition: "the player is currently at <c>locationId</c>". Reads the runtime
    /// <see cref="ICurrentLocationProvider"/> seam (false at the hub). ReasonKey is "locationIs.{locationId}".
    /// </summary>
    public sealed class LocationIsCondition : ICondition
    {
        private readonly ICurrentLocationProvider _provider;
        private readonly string _locationId;

        public LocationIsCondition(ICurrentLocationProvider provider, string locationId)
        {
            _provider = provider;
            _locationId = locationId;
        }

        public ConditionResult Evaluate()
            => ConditionResult.Boolean(
                string.Equals(_provider.CurrentLocationId, _locationId, StringComparison.Ordinal),
                $"locationIs.{_locationId}");
    }
}
