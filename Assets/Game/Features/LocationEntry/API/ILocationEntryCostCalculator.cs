namespace Game.LocationEntry.API
{
    /// <summary>
    /// Computes the per-visit entry fee for a location (base config cost + active decor deltas). Mirror of
    /// the decor sale-chance provider seam: a pure read, no state. The result is what gets charged when the
    /// player commits to entering the location (Preparation confirm) and what the location list shows.
    /// </summary>
    public interface ILocationEntryCostCalculator
    {
        LocationEntryCost Calculate(string locationId);
    }
}
