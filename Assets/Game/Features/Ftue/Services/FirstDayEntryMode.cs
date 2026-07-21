namespace Game.Ftue.Services
{
    /// <summary>
    /// Bootstrap-controlled product switch for how day 1 is entered (see docs/FTUE.md).
    /// <see cref="Hub"/> keeps the classic flow (hub → Start Day → Location Window → Preparation).
    /// <see cref="Location"/> drops the player straight into the location with an auto-stocked shelf.
    /// </summary>
    public enum FirstDayEntryMode
    {
        Hub = 0,
        Location = 1
    }
}
