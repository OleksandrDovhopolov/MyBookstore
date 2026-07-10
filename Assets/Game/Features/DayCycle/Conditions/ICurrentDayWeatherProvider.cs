namespace Game.DayCycle.Conditions
{
    /// <summary>
    /// Read seam for the current game day's weather id, so condition leaves can stay synchronous pure
    /// reads. Backed by the same day resolution as the Morning context (no extra save).
    /// </summary>
    public interface ICurrentDayWeatherProvider
    {
        /// <summary>Weather id of the current day (e.g. "snow"); empty if unresolved.</summary>
        string GetCurrentWeatherId();
    }
}
