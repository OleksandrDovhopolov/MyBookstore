namespace Game.LocationVisits.API
{
    /// <summary>
    /// Write seam for location visits. <see cref="RecordVisit"/> is called once a location entry has
    /// actually succeeded: it increments the persisted per-location counter and sets the current location.
    /// <see cref="ClearCurrentLocation"/> is called on return to the hub.
    /// </summary>
    public interface ILocationVisitService
    {
        void RecordVisit(string locationId);
        void ClearCurrentLocation();
    }
}
