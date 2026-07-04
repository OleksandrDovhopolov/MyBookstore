using System.Collections.Generic;

namespace Game.LocationVisits.API
{
    /// <summary>
    /// Transport DTO between <see cref="ILocationVisitService"/> and its repository. Lifetime visit
    /// count keyed by location id. Current location is runtime-only and intentionally not persisted.
    /// </summary>
    public sealed class LocationVisitsStateDto
    {
        public Dictionary<string, int> VisitsByLocation { get; set; }
    }
}
