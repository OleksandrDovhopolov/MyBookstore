using Game.Conditions.API;
using Game.LocationVisits.API;

namespace Game.LocationVisits.Conditions
{
    /// <summary>
    /// Leaf condition: "location <c>locationId</c> has been entered at least <c>min</c> times".
    /// Reads the read-only <see cref="ILocationVisitsReader"/> seam. ReasonKey is "visitLocation.{locationId}".
    /// </summary>
    public sealed class VisitLocationCondition : ICondition
    {
        private readonly ILocationVisitsReader _reader;
        private readonly string _locationId;
        private readonly int _min;

        public VisitLocationCondition(ILocationVisitsReader reader, string locationId, int min)
        {
            _reader = reader;
            _locationId = locationId;
            _min = min;
        }

        public ConditionResult Evaluate()
            => ConditionResult.Leaf(_reader.GetVisits(_locationId), _min, $"visitLocation.{_locationId}");
    }
}
