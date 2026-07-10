namespace Game.LocationVisits.API
{
    /// <summary>
    /// Runtime current location: the last location entered this session, or <c>null</c> at the hub
    /// (set on entry via <see cref="ILocationVisitService.RecordVisit"/>, cleared on return to hub).
    /// Not persisted — a fresh boot starts at the hub.
    /// </summary>
    public interface ICurrentLocationProvider
    {
        string CurrentLocationId { get; }
    }
}
