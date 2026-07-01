namespace Game.Quest.API
{
    /// <summary>
    /// Lifecycle state of a single quest task (see docs/QUESTS.md §5):
    /// Pending → Active → Completed, with Failed as a side exit.
    /// </summary>
    public enum QuestTaskState
    {
        /// <summary>Waiting for the quest to be active and its own activation conditions to be met.</summary>
        Pending = 0,

        /// <summary>Active; completion conditions are being evaluated.</summary>
        Active = 1,

        /// <summary>Completion conditions satisfied.</summary>
        Completed = 2,

        /// <summary>Failed together with its quest.</summary>
        Failed = 3
    }
}
