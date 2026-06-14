using System.Collections.Generic;

namespace Game.DayCycle.Day
{
    /// <summary>
    /// Shared day progress for the entire core loop. Single source of truth for "what day and phase
    /// the game is in" — read/written by all four phases (Morning, Preparation, Sales, Results).
    /// Persisted as save module <see cref="DayProgressService.ModuleKey"/>.
    /// POCO: serialized by Newtonsoft via ISaveService.UpdateModuleAsync.
    ///
    /// Note: gold lives in Game.Resources (IResourcesService, id "gold") and reputation lives in
    /// Game.Progression (IProgressionService) — both have their own save modules and APIs.
    /// </summary>
    public sealed class DayProgressState
    {
        /// <summary>1-based current game day.</summary>
        public int CurrentDay { get; set; } = 1;

        public DayPhase CurrentPhase { get; set; } = DayPhase.Morning;

        /// <summary>
        /// Completed days — used by Results for idempotent reward application
        /// (prevents double-grant when the player restarts on the summary screen).
        /// </summary>
        public List<int> CompletedDays { get; set; } = new();
    }
}
