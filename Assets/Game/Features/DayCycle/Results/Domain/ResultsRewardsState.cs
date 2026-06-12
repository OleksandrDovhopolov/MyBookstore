using System.Collections.Generic;

namespace Game.DayCycle.Results.Domain
{
    /// <summary>
    /// Save-module payload for the Results phase: the running list of all per-day rewards already
    /// applied. Kept separate from <c>day_progress</c> so the apply path can stay idempotent
    /// without ambiguity about whose write was first.
    /// </summary>
    public sealed class ResultsRewardsState
    {
        public List<AppliedDayRewards> AppliedDays { get; set; } = new();
    }
}
