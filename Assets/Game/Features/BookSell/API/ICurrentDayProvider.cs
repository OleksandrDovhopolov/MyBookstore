namespace Book.Sell.API
{
    /// <summary>
    /// Inversion-of-dependency point so Sales can pick up the current day number without
    /// referencing the DayCycle feature directly. The adapter implementation lives in DayCycle
    /// and reads <c>IDayProgressService.Current.CurrentDay</c>.
    /// </summary>
    public interface ICurrentDayProvider
    {
        int CurrentDay { get; }

        /// <summary>True when the current day has already been completed (Results not yet advanced).</summary>
        bool IsCurrentDayCompleted { get; }
    }
}
