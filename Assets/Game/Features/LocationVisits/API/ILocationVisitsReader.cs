namespace Game.LocationVisits.API
{
    /// <summary>Read seam: how many times a location has been entered (persisted, lifetime).</summary>
    public interface ILocationVisitsReader
    {
        int GetVisits(string locationId);
    }
}
