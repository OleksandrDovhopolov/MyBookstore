using System;
using Book.Sell.API;
using Game.DayCycle.Day;

namespace Game.DayCycle.Results.Services
{
    /// <summary>
    /// Adapter that lets the Sales feature read the current day from the shared
    /// <see cref="IDayProgressService"/> without taking a direct reference on the DayCycle assembly.
    /// The interface itself lives in <c>Book.Sell.API</c>; both Book.Sell and DayCycle reference that.
    /// </summary>
    public sealed class DayProgressCurrentDayProvider : ICurrentDayProvider
    {
        private readonly IDayProgressService _progress;

        public DayProgressCurrentDayProvider(IDayProgressService progress)
        {
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        }

        public int CurrentDay => _progress.Current.CurrentDay;

        /// <summary>
        /// True when today has already been added to <see cref="DayProgressState.CompletedDays"/> —
        /// i.e. Results applied rewards but the player has not yet pressed "Next Day" (which would
        /// advance CurrentDay and reset the phase to Morning). Used by the Sales view on restart
        /// to route straight to the Results screen instead of re-playing the day.
        /// </summary>
        public bool IsCurrentDayCompleted
        {
            get
            {
                var state = _progress.Current;
                return state.CompletedDays.Contains(state.CurrentDay);
            }
        }
    }
}
