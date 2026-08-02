using System;
using Game.Conditions.API;
using Game.LocationVisits.API;
using Newtonsoft.Json.Linq;

namespace Game.LocationVisits.Conditions
{
    /// <summary>
    /// Builds <see cref="VisitLocationCondition"/> from
    /// <c>{ "type": "visitLocation", "locationId": "far_beach", "min": 3 }</c>.
    /// Registered in DI so the condition engine discovers it via the <see cref="IConditionFactory"/> collection.
    /// <c>min</c> defaults to 1 (a bare node means "visited at least once", never always-true).
    /// </summary>
    public sealed class VisitLocationConditionFactory : IConditionFactory, IConditionChangeSource
    {
        public const string TypeId = "visitLocation";

        private readonly ILocationVisitsReader _reader;
        private readonly ILocationVisitChangeSource _changes;

        public VisitLocationConditionFactory(
            ILocationVisitsReader reader,
            ILocationVisitChangeSource changes = null)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _changes = changes;
        }

        public string Type => TypeId;

        public event Action Changed
        {
            add { if (_changes != null) _changes.Changed += value; }
            remove { if (_changes != null) _changes.Changed -= value; }
        }

        public ICondition Create(JObject node)
        {
            var locationId = node.Value<string>("locationId");
            if (string.IsNullOrEmpty(locationId))
                throw new ArgumentException("missing 'locationId'");

            var min = node.Value<int?>("min") ?? 1;
            return new VisitLocationCondition(_reader, locationId, min);
        }
    }
}
