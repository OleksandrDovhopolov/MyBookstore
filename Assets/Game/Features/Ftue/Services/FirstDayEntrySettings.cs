namespace Game.Ftue.Services
{
    /// <summary>Bootstrap-controlled switch for the day-1 entry path (see <see cref="FirstDayEntryMode"/>).</summary>
    public sealed class FirstDayEntrySettings
    {
        public FirstDayEntrySettings(FirstDayEntryMode mode)
        {
            Mode = mode;
        }

        public FirstDayEntryMode Mode { get; }
    }
}
