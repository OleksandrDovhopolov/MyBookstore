using System;
using Game.Conditions.API;
using Game.LocationVisits.API;
using Newtonsoft.Json.Linq;

namespace Game.LocationVisits.Conditions
{
    /// <summary>
    /// Builds <see cref="LocationIsCondition"/> from
    /// <c>{ "type": "locationIs", "locationId": "far_beach" }</c>.
    /// Registered in DI so the condition engine discovers it via the <see cref="IConditionFactory"/> collection.
    /// </summary>
    public sealed class LocationIsConditionFactory : IConditionFactory
    {
        public const string TypeId = "locationIs";

        private readonly ICurrentLocationProvider _provider;

        public LocationIsConditionFactory(ICurrentLocationProvider provider)
            => _provider = provider ?? throw new ArgumentNullException(nameof(provider));

        public string Type => TypeId;

        public ICondition Create(JObject node)
        {
            var locationId = node.Value<string>("locationId");
            if (string.IsNullOrEmpty(locationId))
                throw new ArgumentException("missing 'locationId'");

            return new LocationIsCondition(_provider, locationId);
        }
    }
}
